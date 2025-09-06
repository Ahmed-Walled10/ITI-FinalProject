using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Insightly.Services
{
    public class VerificationCodeService : IVerificationCodeService
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<VerificationCodeService> _logger;
        private readonly Random _random = new Random();

        public VerificationCodeService(IMemoryCache cache, ILogger<VerificationCodeService> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public async Task<string> GenerateCodeAsync(string userId, string purpose = "EmailConfirmation")
        {
            // Generate a 5-digit code
            var code = _random.Next(10000, 99999).ToString();

            // Store the code in cache with 15 minutes expiration
            var cacheKey = $"VerificationCode_{purpose}_{userId}";
            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(15));

            _cache.Set(cacheKey, code, cacheOptions);
            
            _logger.LogInformation("Generated new verification code for userId: {UserId}, purpose: {Purpose}", userId, purpose);

            return await Task.FromResult(code);
        }

        public async Task<bool> ValidateCodeAsync(string userId, string code, string purpose = "EmailConfirmation")
        {
            var cacheKey = $"VerificationCode_{purpose}_{userId}";

            if (_cache.TryGetValue(cacheKey, out string? cachedCode))
            {
                if (cachedCode == code)
                {
                    // Remove the code after successful validation
                    _cache.Remove(cacheKey);
                    _logger.LogInformation("Successfully validated verification code for userId: {UserId}, purpose: {Purpose}", userId, purpose);
                    return await Task.FromResult(true);
                }
                else
                {
                    _logger.LogWarning("Invalid verification code entered for userId: {UserId}, purpose: {Purpose}", userId, purpose);
                }
            }
            else
            {
                _logger.LogWarning("No verification code found or expired for userId: {UserId}, purpose: {Purpose}", userId, purpose);
            }

            return await Task.FromResult(false);
        }

        public async Task InvalidateCodeAsync(string userId, string purpose = "EmailConfirmation")
        {
            var cacheKey = $"VerificationCode_{purpose}_{userId}";
            _cache.Remove(cacheKey);
            _logger.LogInformation("Invalidated verification code for userId: {UserId}, purpose: {Purpose}", userId, purpose);
            await Task.CompletedTask;
        }
    }
}