using System;
using System.Text;
using Unity.Services.Lobbies;

namespace Scene.BattlePVPScene.Service
{
    /// <summary>
    /// PvPマッチング失敗時の例外をユーザー向けメッセージへ変換する
    /// </summary>
    internal static class BattlePvpMatchmakingErrorFormatter
    {
        private const string ServicesSetupGuide =
            "Unity Gaming Servicesが未設定です。UnityエディタでEdit > Project Settings > Servicesからプロジェクトをリンクし、DashboardでAuthentication・Lobby・Relayを有効化してください";

        /// <summary>
        /// 例外から表示用メッセージを生成する
        /// </summary>
        public static string Format(Exception exception)
        {
            if (exception == null)
            {
                return "不明なエラーが発生しました";
            }

            if (exception is AggregateException aggregateException
                && aggregateException.InnerExceptions.Count > 0)
            {
                return Format(aggregateException.InnerExceptions[0]);
            }

            if (exception is LobbyServiceException lobbyException)
            {
                return $"マッチングエラー: {lobbyException.Message}";
            }

            string message = exception.Message ?? string.Empty;
            if (IsServicesConfigurationError(message))
            {
                return ServicesSetupGuide;
            }

            if (exception.InnerException != null)
            {
                string innerMessage = Format(exception.InnerException);
                if (!string.Equals(innerMessage, message, StringComparison.Ordinal))
                {
                    return CombineMessages(message, innerMessage);
                }
            }

            return message;
        }

        private static bool IsServicesConfigurationError(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            return message.Contains("Some services", StringComparison.OrdinalIgnoreCase)
                || message.Contains("couldn't be initialized", StringComparison.OrdinalIgnoreCase)
                || message.Contains("cloud project", StringComparison.OrdinalIgnoreCase)
                || message.Contains("project id", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Project ID", StringComparison.Ordinal)
                || message.Contains("UnityProjectNotLinked", StringComparison.OrdinalIgnoreCase);
        }

        private static string CombineMessages(string outerMessage, string innerMessage)
        {
            if (IsServicesConfigurationError(outerMessage))
            {
                return innerMessage;
            }

            if (IsServicesConfigurationError(innerMessage))
            {
                return innerMessage;
            }

            StringBuilder builder = new StringBuilder(outerMessage.Length + innerMessage.Length + 1);
            builder.Append(outerMessage);
            builder.Append('\n');
            builder.Append(innerMessage);
            return builder.ToString();
        }
    }
}
