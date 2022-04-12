﻿namespace Adnc.Shared.Application.BloomFilter;

public class BloomFilterHostedService : BackgroundService
{
    private readonly ILogger<BloomFilterHostedService> _logger;
    private readonly ICacheProvider _cache;
    private readonly IEnumerable<IBloomFilter> _bloomFilters;
    private readonly ICachePreheatable _cacheService;

    public BloomFilterHostedService(ILogger<BloomFilterHostedService> logger
       , ICacheProvider cache
       , IEnumerable<IBloomFilter> bloomFilters
       , ICachePreheatable cacheService)
    {
        _logger = logger;
        _cache = cache;
        _bloomFilters = bloomFilters;
        _cacheService = cacheService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        #region Init BloomFilter

        if (_bloomFilters.IsNotNullOrEmpty())
        {
            foreach (var filter in _bloomFilters)
            {
                await filter.InitAsync();
            }
        }

        #endregion Init BloomFilter

        #region Init Caches

        await _cacheService.PreheatAsync();

        #endregion Init Caches

        #region Confirm Caching Removed

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!LocalVariables.Instance.Queue.TryDequeue(out LocalVariables.Model model)
                || model.CacheKeys?.Any() == false
                || DateTime.Now > model.ExpireDt)
            {
                await Task.Delay(_cache.CacheOptions.LockMs, stoppingToken);
                continue;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (DateTime.Now > model.ExpireDt) break;

                    await _cache.RemoveAllAsync(model.CacheKeys);

                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.Message, ex);
                    await Task.Delay(_cache.CacheOptions.LockMs, stoppingToken);
                }
            }
        }

        #endregion Confirm Caching Removed
    }
}