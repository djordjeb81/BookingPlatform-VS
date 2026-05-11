using System;
using System.Threading;
using System.Threading.Tasks;
using SmartBooking_Desk.Models.Licensing;

namespace SmartBooking_Desk.Services.Licensing
{
    public class LicenseGuardService
    {
        private readonly HwidService _hwidService;
        private readonly LocalLicenseCacheService _localCacheService;
        private readonly LicenseApiClient _licenseApiClient;

        public LicenseGuardService()
        {
            _hwidService = new HwidService();
            _localCacheService = new LocalLicenseCacheService();
            _licenseApiClient = new LicenseApiClient();
        }

        public async Task<LicenseCheckResultDto> EnsureLicenseAsync(
            string email,
            string jwtToken,
            CancellationToken cancellationToken = default)
        {
            var state = _localCacheService.Load();

            state.Email = email;
            state.HwidHash = _hwidService.GetHwidHash();
            state.ComputerName = _hwidService.GetComputerName();
            state.ProgramVersion = _hwidService.GetProgramVersion();

            _licenseApiClient.SetBearerToken(jwtToken);

            if (!state.IsRegistered)
            {
                try
                {
                    var registerResponse = await _licenseApiClient.RegisterDeviceAsync(
                        new RegisterDeviceRequestDto
                        {
                            HwidHash = state.HwidHash,
                            ComputerName = state.ComputerName,
                            ProgramVersion = state.ProgramVersion
                        },
                        cancellationToken);

                    state.IsRegistered = true;
                    state.Status = registerResponse.Status ?? "";
                    state.ValidUntilUtc = registerResponse.ValidUntilUtc;
                    state.LastOnlineAttemptUtc = DateTime.UtcNow;
                    state.IsApproved = string.Equals(registerResponse.Status, "Approved", StringComparison.OrdinalIgnoreCase);

                    _localCacheService.Save(state);

                    if (string.Equals(registerResponse.Status, "Approved", StringComparison.OrdinalIgnoreCase))
                    {
                        return await RefreshAndPersistAsync(state, cancellationToken);
                    }

                    if (string.Equals(registerResponse.Status, "Blocked", StringComparison.OrdinalIgnoreCase))
                    {
                        return new LicenseCheckResultDto
                        {
                            IsAllowed = false,
                            IsBlocked = true,
                            Message = "Ovaj uređaj je blokiran."
                        };
                    }

                    return new LicenseCheckResultDto
                    {
                        IsAllowed = false,
                        IsPendingApproval = true,
                        Message = "Uređaj je prijavljen i čeka odobrenje licence."
                    };
                }
                catch (Exception ex)
                {
                    return new LicenseCheckResultDto
                    {
                        IsAllowed = false,
                        Message = $"Neuspešna prijava uređaja: {ex.Message}"
                    };
                }
            }

            if (!state.ShouldTryOnlineRefresh())
            {
                if (state.CanWorkOffline())
                {
                    return new LicenseCheckResultDto
                    {
                        IsAllowed = true,
                        IsOfflineMode = false,
                        Message = "Licenca je važeća."
                    };
                }

                return new LicenseCheckResultDto
                {
                    IsAllowed = false,
                    Message = "Licenca je istekla."
                };
            }

            try
            {
                return await RefreshAndPersistAsync(state, cancellationToken);
            }
            catch
            {
                state.LastOnlineAttemptUtc = DateTime.UtcNow;
                _localCacheService.Save(state);

                if (state.CanWorkOffline())
                {
                    return new LicenseCheckResultDto
                    {
                        IsAllowed = true,
                        IsOfflineMode = true,
                        Message = "Nije uspela online provera licence. Aplikacija nastavlja rad u offline režimu."
                    };
                }

                return new LicenseCheckResultDto
                {
                    IsAllowed = false,
                    Message = "Licenca je istekla i online provera nije uspela."
                };
            }
        }

        private async Task<LicenseCheckResultDto> RefreshAndPersistAsync(
            LocalLicenseStateDto state,
            CancellationToken cancellationToken)
        {
            var refreshResponse = await _licenseApiClient.RefreshAsync(
                new RefreshLicenseRequestDto
                {
                    HwidHash = state.HwidHash,
                    ComputerName = state.ComputerName,
                    ProgramVersion = state.ProgramVersion
                },
                cancellationToken);

            state.LastOnlineAttemptUtc = DateTime.UtcNow;
            state.Status = refreshResponse.Status ?? "";
            state.IsApproved = refreshResponse.IsApproved;
            state.LastSuccessfulLicenseCheckUtc = DateTime.UtcNow;
            state.LastLicenseRefreshAtUtc = refreshResponse.LastLicenseRefreshAtUtc;
            state.ValidUntilUtc = refreshResponse.ValidUntilUtc;
            state.LicenseToken = refreshResponse.LicenseToken ?? "";

            _localCacheService.Save(state);

            if (string.Equals(refreshResponse.Status, "Blocked", StringComparison.OrdinalIgnoreCase))
            {
                return new LicenseCheckResultDto
                {
                    IsAllowed = false,
                    IsBlocked = true,
                    Message = "Ovaj uređaj je blokiran."
                };
            }

            if (!refreshResponse.IsApproved)
            {
                return new LicenseCheckResultDto
                {
                    IsAllowed = false,
                    IsPendingApproval = true,
                    Message = "Licenca za ovaj uređaj još nije odobrena."
                };
            }

            return new LicenseCheckResultDto
            {
                IsAllowed = true,
                Message = "Licenca je uspešno proverena."
            };
        }
    }
}