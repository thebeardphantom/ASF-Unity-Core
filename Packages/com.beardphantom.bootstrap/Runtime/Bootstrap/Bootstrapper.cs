﻿using UnityEngine;
using UnityEngine.Assertions;

namespace BeardPhantom.Bootstrap
{
    public sealed partial class Bootstrapper : MonoBehaviour
    {
        private IPreBootstrapHandler _preHandler;

        private IPostBootstrapHandler _postHandler;

        private bool _isOverrideInstance;

        [field: SerializeField]
        internal PrefabProvider PrefabProvider { get; set; }

        private void AssignBootstrapHandlers()
        {
            bool foundPreHandler = TryGetComponent(out _preHandler);
            bool foundPostHandler = TryGetComponent(out _postHandler);

            BootstrapUtility.GetDefaultBootstrapHandlers(out IPreBootstrapHandler defautlPreHandler, out IPostBootstrapHandler defaultPostHandler);
            _preHandler = foundPreHandler ? _preHandler : defautlPreHandler;
            _postHandler = foundPostHandler ? _postHandler : defaultPostHandler;

            Log.Verbose($"Selected IPreBootstrapHandler {_preHandler}.", this);
            Log.Verbose($"Selected IPostBootstrapHandler {_postHandler}.", this);
        }

        private void Start()
        {
            BootstrapApplicationAsync().Forget();
        }

        private async Awaitable BootstrapApplicationAsync()
        {
            Assert.IsTrue(gameObject.scene.buildIndex == 0, "gameObject.scene.buildIndex == 0");

#if UNITY_EDITOR
            if (!_isOverrideInstance)
            {
                TryReplaceWithOverrideInstance();
                if (this == null)
                {
                    // this instance was destroyed
                    return;
                }
            }
#endif

            var context = new BootstrapContext(this);
            Assert.IsNotNull(PrefabProvider, "ServicesPrefabLoader != null");

            App.BootstrapState = AppBootstrapState.BootstrapHandlerDiscovery;
            Log.Info("Bootstrapping application.", this);
            AssignBootstrapHandlers();

            App.BootstrapState = AppBootstrapState.PreBootstrap;
            Log.Verbose("Beginning pre-bootstrapping.", this);
            await _preHandler.OnPreBootstrapAsync(context);
            await Awaitable.NextFrameAsync();

            App.BootstrapState = AppBootstrapState.ServicePrefabLoad;
            Log.Verbose($"Loading services prefab via loader {PrefabProvider}.", this);
            GameObject servicesPrefab = await PrefabProvider.LoadPrefabAsync();

            App.BootstrapState = AppBootstrapState.ServiceCreation;
            Log.Verbose("Creating services.", this);
            servicesPrefab.SetActive(false);
            GameObject servicesInstance = Instantiate(servicesPrefab);
            DontDestroyOnLoad(servicesInstance);
            servicesInstance.name = servicesPrefab.name;
            servicesPrefab.SetActive(true);
            BootstrapUtility.ClearDirtyFlag(servicesPrefab);
            await App.ServiceLocator.CreateAsync(context, servicesInstance, HideFlags.None);
            await Awaitable.NextFrameAsync();

            App.BootstrapState = AppBootstrapState.PostBoostrap;
            Log.Verbose("Beginning post-bootstrapping.", this);
            await _postHandler.OnPostBootstrapAsync(context, this);
            await Awaitable.NextFrameAsync();

            App.BootstrapState = AppBootstrapState.Ready;
            Log.Info("Bootstrapping complete.", this);
        }

        partial void TryReplaceWithOverrideInstance();
    }
}