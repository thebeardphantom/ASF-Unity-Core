﻿using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BeardPhantom.Fabric.Core
{
    public class ServiceLocator : IServiceLocator
    {
        #region Fields

        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

        #endregion

        #region Methods

        public async UniTask CreateAsync(GameObject prefab)
        {
            prefab.SetActive(false);
            var services = Object.Instantiate(prefab);
            prefab.SetActive(true);

            Object.DontDestroyOnLoad(services);

            // Bind all services
            using (ListPool<MonoBehaviour>.Get(out var foundServices))
            {
                services.GetComponentsInChildren(true, foundServices);
                foreach (var service in foundServices)
                {
                    var serviceType = service.GetType();
                    _services.Add(serviceType, service);
                    if (service is IFabricService fabricService)
                    {
                        using (ListPool<Type>.Get(out var extraBindableTypes))
                        {
                            fabricService.GetExtraBindableTypes(extraBindableTypes);
                            foreach (var extraType in extraBindableTypes)
                            {
                                _services.Add(extraType, service);
                            }
                        }
                    }
                }
            }

            // Call OnCreateService on each service
            using (ListPool<UniTask>.Get(out var tasks))
            {
                foreach (var service in _services.Values)
                {
                    if (service is IFabricService fabricService)
                    {
                        tasks.Add(fabricService.OnCreateServiceAsync());
                    }
                }

                await UniTask.WhenAll(tasks);
            }

            // Call OnAllServicesCreatedAsync on each service
            using (ListPool<UniTask>.Get(out var tasks))
            {
                foreach (var service in _services.Values)
                {
                    if (service is IFabricService fabricService)
                    {
                        tasks.Add(fabricService.OnAllServicesCreatedAsync());
                    }
                }

                await UniTask.WhenAll(tasks);
            }

            services.SetActive(true);
        }

        public bool TryLocateService<T>(out T service) where T : class
        {
            if (_services.TryGetValue(typeof(T), out var rawObject))
            {
                service = rawObject as T;
            }
            else
            {
                service = default;
            }

            return service.IsNotNull();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            foreach (var service in _services.Values)
            {
                if (service is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }

        #endregion
    }
}