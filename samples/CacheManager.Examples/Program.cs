﻿using System;
using CacheManager.Core;
using CacheManager.Core.Configuration;
using Microsoft.Practices.Unity;

namespace CacheManager.Examples
{
    public class Program
    {
        private static void AppConfigLoadInstalledCacheCfg()
        {
            var cache = CacheFactory.FromConfiguration<object>("myCache");
            cache.Add("key", "value");
        }

        private static void EventsExample()
        {
            var cache = CacheFactory.FromConfiguration<object>("myCache");
            cache.OnAdd += (sender, args) => Console.WriteLine("Added " + args.Key);
            cache.OnGet += (sender, args) => Console.WriteLine("Got " + args.Key);
            cache.OnRemove += (sender, args) => Console.WriteLine("Removed " + args.Key);

            cache.Add("key", "value");
            var val = cache.Get("key");
            cache.Remove("key");
        }

        private static void Main(string[] args)
        {
            EventsExample();
            UnityInjectionExample();
            UnityInjectionExample_Advanced();
            AppConfigLoadInstalledCacheCfg();
            SimpleCustomBuildConfigurationUsingConfigBuilder();
            SimpleCustomBuildConfigurationUsingFactory();
            UpdateTest();
            UpdateCounterTest();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        private static void RedisSample()
        {
            var cache = CacheFactory.Build<int>("myCache", settings =>
            {
                settings
                    .WithSystemRuntimeCacheHandle("inProcessCache")
                    .And
                    .WithRedisConfiguration("redis", config =>
                    {
                        config.WithAllowAdmin()
                            .WithDatabase(0)
                            .WithEndpoint("localhost", 6379);
                    })
                    .WithMaxRetries(1000)
                    .WithRetryTimeout(100)
                    .WithRedisBackPlate("redis")
                    .WithRedisCacheHandle("redis", true);
            });

            cache.Add("test", 123456);

            cache.Update("test", p => p + 1);

            var result = cache.Get("test");
        }

        private static void SimpleCustomBuildConfigurationUsingConfigBuilder()
        {
            // this is using the CacheManager.Core.Configuration.ConfigurationBuilder to build a
            // custom config you can do the same with the CacheFactory
            var cfg = ConfigurationBuilder.BuildConfiguration(settings =>
                {
                    settings.WithUpdateMode(CacheUpdateMode.Up)
                        .WithSystemRuntimeCacheHandle("handle1")
                            .EnablePerformanceCounters()
                            .WithExpiration(ExpirationMode.Sliding, TimeSpan.FromSeconds(10));
                });

            var cache = CacheFactory.FromConfiguration<string>("stringCache", cfg);
            cache.Add("key", "value");

            // reusing the configuration and using the same cache for different types:
            var numbers = CacheFactory.FromConfiguration<int>("numberCache", cfg);
            numbers.Add("intKey", 2323);
            numbers.Update("intKey", v => v + 1);
        }

        private static void SimpleCustomBuildConfigurationUsingFactory()
        {
            var cache = CacheFactory.Build("myCacheName", settings =>
            {
                settings
                    .WithUpdateMode(CacheUpdateMode.Up)
                    .WithSystemRuntimeCacheHandle("handle1")
                        .EnablePerformanceCounters()
                        .WithExpiration(ExpirationMode.Sliding, TimeSpan.FromSeconds(10));
            });

            cache.Add("key", "value");
        }

        private static void UnityInjectionExample()
        {
            UnityContainer container = new UnityContainer();
            container.RegisterType<ICacheManager<object>>(
                new ContainerControlledLifetimeManager(),
                new InjectionFactory((c) => CacheFactory.FromConfiguration<object>("myCache")));

            container.RegisterType<UnityInjectionExampleTarget>();

            // resolving the test target object should also resolve the cache instance
            var target = container.Resolve<UnityInjectionExampleTarget>();
            target.PutSomethingIntoTheCache();

            // our cache manager instance should still be there so should the object we added in the
            // previous step.
            var checkTarget = container.Resolve<UnityInjectionExampleTarget>();
            checkTarget.GetSomething();
        }

        private static void UnityInjectionExample_Advanced()
        {
            UnityContainer container = new UnityContainer();
            container.RegisterType(
                typeof(ICacheManager<>),
                new ContainerControlledLifetimeManager(),
                new InjectionFactory(
                    (c, t, n) => CacheFactory.FromConfiguration(t.GetGenericArguments()[0],
                    "myCache",
                    ConfigurationBuilder.BuildConfiguration(cfg => cfg.WithSystemRuntimeCacheHandle("handle1")))));

            var stringCache = container.Resolve<ICacheManager<string>>();
            // testing if we create a singleton instance per type, every Resolve of the same type should return the same instance!
            var stringCacheB = container.Resolve<ICacheManager<string>>();  
            stringCache.Put("key", "something");

            var intCache = container.Resolve<ICacheManager<int>>();
            var intCacheB = container.Resolve<ICacheManager<int>>();
            intCache.Put("key", 22);

            var boolCache = container.Resolve<ICacheManager<bool>>();
            var boolCacheB = container.Resolve<ICacheManager<bool>>();
            boolCache.Put("key", false);

            Console.WriteLine("Value type is: " + stringCache.GetType().GetGenericArguments()[0].Name + " test value: " + stringCacheB["key"]);
            Console.WriteLine("Value type is: " + intCache.GetType().GetGenericArguments()[0].Name + " test value: " + intCacheB["key"]);
            Console.WriteLine("Value type is: " + boolCache.GetType().GetGenericArguments()[0].Name + " test value: " + boolCacheB["key"]);
        }

        private static void UpdateTest()
        {
            var cache = CacheFactory.Build<string>("myCache", s => s.WithSystemRuntimeCacheHandle("handle"));

            Console.WriteLine("Testing update...");

            string newValue;
            if (!cache.TryUpdate("test", v => "item has not yet been added", out newValue))
            {
                Console.WriteLine("Value not added?: {0}", newValue == null);
            }

            cache.Add("test", "start");
            Console.WriteLine("Inital value: {0}", cache["test"]);

            cache.AddOrUpdate("test", "adding again?", v => "updating and not adding");
            Console.WriteLine("After AddOrUpdate: {0}", cache["test"]);

            cache.Remove("test");
            var removeValue = cache.Update("test", v => "updated?");
            Console.WriteLine("Value after remove is null?: {0}", removeValue == null);
        }

        private static void UpdateCounterTest()
        {
            var cache = CacheFactory.Build<long>("myCache", s => s.WithSystemRuntimeCacheHandle("handle"));

            Console.WriteLine("Testing update counter...");

            cache.AddOrUpdate("counter", 0, v => v + 1);

            Console.WriteLine("Initial value: {0}", cache.Get("counter"));

            for (int i = 0; i < 12345; i++)
            {
                cache.Update("counter", v => v + 1);
            }

            Console.WriteLine("Final value: {0}", cache.Get("counter"));
        }
    }
    
    public class UnityInjectionExampleTarget
    {
        private ICacheManager<object> cache;

        public UnityInjectionExampleTarget(ICacheManager<object> cache)
        {
            if (cache == null)
            {
                throw new ArgumentNullException("cache");
            }

            this.cache = cache;
        }

        public void GetSomething()
        {
            var value = this.cache.Get("myKey");
            var x = value;
            if (value == null)
            {
                throw new InvalidOperationException();
            }
        }

        public void PutSomethingIntoTheCache()
        {
            this.cache.Put("myKey", "something");
        }
    }
}