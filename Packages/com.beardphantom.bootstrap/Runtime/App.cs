﻿using System;
using UnityEngine;
using UnityEngine.Assertions;

namespace BeardPhantom.Bootstrap
{
    public static partial class App
    {
        public delegate void OnAppBootstrapStateChanged(AppBootstrapState previousState, AppBootstrapState newState);

        public static event OnAppBootstrapStateChanged AppBootstrapStateChanged;

        private static AppBootstrapState s_bootstrapState;

        public static AppBootstrapState BootstrapState
        {
            get => s_bootstrapState;
            internal set
            {
                if (s_bootstrapState == value)
                {
                    return;
                }

                AppBootstrapState previousState = s_bootstrapState;
                s_bootstrapState = value;
                AppBootstrapStateChanged?.Invoke(previousState, value);
            }
        }

        public static ServiceLocator ServiceLocator { get; private set; }

        public static AppInitMode InitMode { get; set; }

        public static Guid SessionGuid { get; private set; }

        public static bool IsPlaying { get; private set; }

        public static bool IsQuitting { get; private set; }

        public static bool IsRunningTests { get; internal set; }

        public static bool CanLocateServices => ServiceLocator is { CanLocateServices: true, };

        public static bool TryLocate<T>(out T service) where T : class
        {
            if (!CanLocateServices)
            {
                service = default;
                return false;
            }

            return ServiceLocator.TryLocateService(out service);
        }

        public static T Locate<T>() where T : class
        {
            Assert.IsTrue(CanLocateServices, "CanLocateServices");
            return ServiceLocator.LocateService<T>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void Init()
        {
            InitMode = BootstrapUtility.IsInPlayMode() ? AppInitMode.PlayMode : AppInitMode.EditMode;
            s_bootstrapState = AppBootstrapState.None;
            IsQuitting = false;
            IsPlaying = InitMode == AppInitMode.PlayMode;
            if (InitMode == AppInitMode.PlayMode)
            {
                Application.quitting -= OnApplicationQuitting;
                Application.quitting += OnApplicationQuitting;
            }

            ServiceLocator = new ServiceLocator();
        }

        private static void OnApplicationQuitting()
        {
            Application.quitting -= OnApplicationQuitting;
            IsQuitting = true;
        }
    }
}