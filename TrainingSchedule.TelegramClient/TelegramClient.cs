﻿using System.Data;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TrainingSchedule.Domain;
using TrainingSchedule.Domain.Entities;

namespace TrainingSchedule.Telegram
{
    public class TelegramClient : IBotClient
    {
        public event Func<long, long, string, Task>? MessageReceived;

        private readonly TelegramBotClient _botClient;

        public TelegramClient()
        {
            var tgToken = Environment.GetEnvironmentVariable("tgToken", EnvironmentVariableTarget.User) ?? throw new ArgumentNullException("Не удалось получить токен.");

            if (tgToken is null)
            {
                throw new ArgumentNullException("Не задан токен.");
            }

            _botClient = new TelegramBotClient(tgToken);
        }

        public async Task StartAsync()
        {
            using var cts = new CancellationTokenSource();

            // StartReceiving does not block the caller thread. Receiving is done on the ThreadPool.
            var receiverOptions = new ReceiverOptions
            {
                // receive all update types
                AllowedUpdates = Array.Empty<UpdateType>()
            };
            _botClient.StartReceiving(
                        updateHandler: HandleUpdateAsync,
                        pollingErrorHandler: HandlePollingErrorAsync,
                        receiverOptions: receiverOptions,
                        cancellationToken: cts.Token
                    );

            var me = await _botClient.GetMeAsync();

            Console.WriteLine($"Start listening for @{me.Username}");

            //cts.Cancel();
        }

        public async Task SendMessageAsync(long chatId, string message)
        {
            Message sentMessage = await _botClient.SendTextMessageAsync(
                chatId: chatId,
                text: message,
                cancellationToken: default);
        }

        public async Task SendMessageAsync(long chatId, string message, IAllowedAnswers allowedAnswers)
        {
            var buttons = allowedAnswers.Items.Select((item) => InlineKeyboardButton.WithCallbackData($"{item.Name}", item.Value));

            var buttonsList = new List<List<InlineKeyboardButton>>();

            foreach (var button in buttons)
            {
                buttonsList.Add(new List<InlineKeyboardButton> { button });
            }

            var inlineKeyboard = new InlineKeyboardMarkup(buttonsList);

            Message sentMessage = await _botClient.SendTextMessageAsync(
                chatId: chatId,
                text: message,
                replyMarkup: inlineKeyboard,
                cancellationToken: default);
        }

        private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            long chatId, userId;
            string? messageText;

            if (update.Message?.Text is not null)
            {
                var message = update.Message;
                chatId = message.Chat.Id;
                userId = message.From.Id;
                messageText = update.Message.Text;

                Console.WriteLine($"Received a '{messageText}' message in chat {chatId}.");

                MessageReceived?.Invoke(userId, chatId, messageText);
            }
            else if (update.CallbackQuery is not null)
            {
                var callbackQuery = update.CallbackQuery;
                chatId = callbackQuery.Message.Chat.Id;
                userId = callbackQuery.From.Id;
                messageText = callbackQuery.Data;

                Console.WriteLine($"Received a '{messageText}' message in chat {chatId}.");

                MessageReceived?.Invoke(userId, chatId, messageText);

                await botClient.AnswerCallbackQueryAsync(update.CallbackQuery.Id);
            }
        }

        private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            var ErrorMessage = exception switch
            {
                ApiRequestException apiRequestException
                    => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };

            Console.WriteLine(ErrorMessage);

            return Task.CompletedTask;
        }
    }
}