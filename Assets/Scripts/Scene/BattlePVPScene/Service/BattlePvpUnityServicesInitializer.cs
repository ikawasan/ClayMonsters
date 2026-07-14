using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

namespace Scene.BattlePVPScene.Service
{
    /// <summary>
    /// Unity Gaming Servicesの初期化と匿名認証を行う
    /// </summary>
    public static class BattlePvpUnityServicesInitializer
    {
        private const string ServicesSetupGuide =
            "Unity Gaming Servicesが未設定です。UnityエディタでEdit > Project Settings > Servicesからプロジェクトをリンクし、DashboardでAuthentication・Lobby・Relayを有効化してください";

        private static bool isInitialized;

        /// <summary>
        /// RelayとLobby利用前にサービスを初期化する
        /// </summary>
        public static async UniTask EnsureInitializedAsync(CancellationToken cancellationToken)
        {
            if (isInitialized && AuthenticationService.Instance.IsSignedIn)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Application.cloudProjectId))
            {
                throw new InvalidOperationException(ServicesSetupGuide);
            }

            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                {
                    await UnityServices.InitializeAsync()
                        .AsUniTask()
                        .AttachExternalCancellation(cancellationToken);
                }

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync()
                        .AsUniTask()
                        .AttachExternalCancellation(cancellationToken);
                }

                isInitialized = true;
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(FormatInitializationError(exception), exception);
            }
        }

        private static string FormatInitializationError(Exception exception)
        {
            string detail = BattlePvpMatchmakingErrorFormatter.Format(exception);
            if (detail.Contains(ServicesSetupGuide, StringComparison.Ordinal))
            {
                return detail;
            }

            return $"{ServicesSetupGuide}\n{detail}";
        }
    }
}
