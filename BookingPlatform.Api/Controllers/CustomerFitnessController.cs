using BookingPlatform.Contracts.Fitness;
using BookingPlatform.Domain.Fitness;
using BookingPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookingPlatform.Api.Controllers;

[ApiController]
[Route("api/customer-fitness")]
public sealed class CustomerFitnessController : ControllerBase
{
    private readonly BookingDbContext _db;

    public CustomerFitnessController(BookingDbContext db)
    {
        _db = db;
    }

    // ============================================================
    // CUSTOMER SCHEDULE / RASPORED ZA KLIJENTA
    // ============================================================

    [HttpGet("businesses/{businessId:long}/sessions")]
    public async Task<ActionResult<List<CustomerFitnessSessionDto>>> GetSessions(
        long businessId,
        [FromQuery] long? appUserId,
        [FromQuery] long? customerProfileId,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc)
    {
        var business = await _db.Businesses
            .FirstOrDefaultAsync(x => x.Id == businessId);

        if (business is null)
        {
            return NotFound("Biznis nije pronađen.");
        }

        var from = fromUtc ?? DateTime.UtcNow.Date;
        var to = toUtc ?? from.AddDays(7);

        if (to <= from)
        {
            return BadRequest("Datum završetka mora biti posle datuma početka.");
        }

        var sessions = await _db.FitnessSessions
            .Include(x => x.FitnessRoom)
            .Include(x => x.FitnessClassType)
            .Include(x => x.Bookings)
            .Where(x =>
                x.BusinessId == businessId &&
                x.Status == FitnessSessionStatus.Scheduled &&
                x.StartAtUtc >= from &&
                x.StartAtUtc < to)
            .OrderBy(x => x.StartAtUtc)
            .ThenBy(x => x.FitnessRoom.Name)
            .ToListAsync();

        var result = sessions
            .Select(x => ToCustomerSessionDto(
                x,
                business.Name,
                appUserId,
                customerProfileId))
            .ToList();

        return Ok(result);
    }

    // ============================================================
    // CUSTOMER SESSION DETAILS / DETALJ TERMINA ZA KLIJENTA
    // ============================================================

    [HttpGet("sessions/{sessionId:long}/details")]
    public async Task<ActionResult<CustomerFitnessSessionDetailsDto>> GetSessionDetails(
        long sessionId,
        [FromQuery] long? appUserId,
        [FromQuery] long? customerProfileId)
    {
        if (sessionId <= 0)
        {
            return BadRequest("sessionId je obavezan.");
        }

        var session = await _db.FitnessSessions
            .Include(x => x.FitnessRoom)
            .Include(x => x.FitnessClassType)
            .Include(x => x.Bookings)
            .FirstOrDefaultAsync(x => x.Id == sessionId);

        if (session is null)
        {
            return NotFound("Termin nije pronađen.");
        }

        var business = await _db.Businesses
            .FirstOrDefaultAsync(x => x.Id == session.BusinessId);

        if (business is null)
        {
            return NotFound("Biznis nije pronađen.");
        }

        var participants = session.Bookings
            .Where(x =>
                x.Status == FitnessSessionBookingStatus.Booked ||
                x.Status == FitnessSessionBookingStatus.PendingApproval ||
                x.Status == FitnessSessionBookingStatus.Attended)
            .OrderBy(x => x.CreatedAtUtc)
            .Select(x => new CustomerFitnessSessionParticipantDto
            {
                BookingId = x.Id,
                DisplayName = BuildCustomerPublicDisplayName(x.CustomerName),
                BookingStatus = (int)x.Status,
                BookingStatusText = GetBookingStatusText(x.Status)
            })
            .ToList();

        return Ok(new CustomerFitnessSessionDetailsDto
        {
            Session = ToCustomerSessionDto(
                session,
                business.Name,
                appUserId,
                customerProfileId),
            Participants = participants
        });
    }

    // ============================================================
    // CUSTOMER MY FITNESS BOOKINGS / MOJI TERMINI TERETANE
    // ============================================================

    [HttpGet("my-bookings")]
    public async Task<ActionResult<List<CustomerFitnessBookingDto>>> GetMyBookings(
      [FromQuery] long? appUserId,
      [FromQuery] long? customerProfileId,
      [FromQuery] DateTime? fromUtc,
      [FromQuery] DateTime? toUtc,
      CancellationToken cancellationToken)
    {
        var from = fromUtc ?? DateTime.UtcNow.Date.AddDays(-1);
        var to = toUtc ?? DateTime.UtcNow.Date.AddDays(60);

        if (!appUserId.HasValue && !customerProfileId.HasValue)
        {
            return BadRequest("Nedostaje klijent za prikaz fitness termina.");
        }

        var query = _db.FitnessSessionBookings
            .AsNoTracking()
            .Include(x => x.FitnessSession)
                .ThenInclude(x => x.FitnessRoom)
            .Include(x => x.FitnessSession)
                .ThenInclude(x => x.FitnessClassType)
            .Include(x => x.FitnessSession)
                .ThenInclude(x => x.Business)
            .Where(x =>
                x.FitnessSession.StartAtUtc >= from &&
                x.FitnessSession.StartAtUtc < to &&
                (
                    x.Status == FitnessSessionBookingStatus.Booked ||
                    x.Status == FitnessSessionBookingStatus.PendingApproval ||
                    x.Status == FitnessSessionBookingStatus.Attended
                ));

        if (customerProfileId.HasValue && appUserId.HasValue)
        {
            query = query.Where(x =>
                x.CustomerProfileId == customerProfileId.Value ||
                x.AppUserId == appUserId.Value);
        }
        else if (customerProfileId.HasValue)
        {
            query = query.Where(x => x.CustomerProfileId == customerProfileId.Value);
        }
        else if (appUserId.HasValue)
        {
            query = query.Where(x => x.AppUserId == appUserId.Value);
        }

        var bookings = await query
            .OrderBy(x => x.FitnessSession.StartAtUtc)
            .ToListAsync(cancellationToken);

        var sessionIds = bookings
            .Select(x => x.FitnessSessionId)
            .Distinct()
            .ToList();

        var bookedCounts = await _db.FitnessSessionBookings
            .AsNoTracking()
            .Where(x =>
                sessionIds.Contains(x.FitnessSessionId) &&
                (
                    x.Status == FitnessSessionBookingStatus.Booked ||
                    x.Status == FitnessSessionBookingStatus.PendingApproval ||
                    x.Status == FitnessSessionBookingStatus.Attended
                ))
            .GroupBy(x => x.FitnessSessionId)
            .Select(g => new
            {
                FitnessSessionId = g.Key,
                Count = g.Count()
            })
            .ToDictionaryAsync(
                x => x.FitnessSessionId,
                x => x.Count,
                cancellationToken);

        var result = bookings
            .Select(x =>
            {
                var session = x.FitnessSession;

                bookedCounts.TryGetValue(session.Id, out var bookedCount);

                return new CustomerFitnessBookingDto
                {
                    BookingId = x.Id,
                    FitnessSessionId = session.Id,
                    BusinessId = session.BusinessId,
                    BusinessName = session.Business?.Name ?? string.Empty,

                    FitnessRoomId = session.FitnessRoomId,
                    FitnessRoomName = session.FitnessRoom?.Name ?? string.Empty,

                    FitnessClassTypeId = session.FitnessClassTypeId,
                    FitnessClassTypeName = session.FitnessClassType?.Name,

                    TrainerName = null,

                    SessionType = (int)session.SessionType,
                    SessionTypeText = GetSessionTypeText(session.SessionType),

                    StartAtUtc = session.StartAtUtc,
                    EndAtUtc = session.EndAtUtc,

                    Capacity = session.Capacity,
                    BookedCount = bookedCount,
                    CapacityText = $"{bookedCount}/{session.Capacity}",

                    BookingStatus = (int)x.Status,
                    BookingStatusText = GetBookingStatusText(x.Status),

                    Note = session.Note
                };
            })
            .ToList();

        return Ok(result);
    }

    // ============================================================
    // CUSTOMER BOOKING / PRIJAVA KLIJENTA
    // ============================================================

    [HttpPost("businesses/{businessId:long}/bookings")]
    public async Task<ActionResult<CustomerCreateFitnessBookingResponse>> CreateBooking(
        long businessId,
        CustomerCreateFitnessBookingRequest request)
    {
        var business = await _db.Businesses
            .FirstOrDefaultAsync(x => x.Id == businessId);

        if (business is null)
        {
            return NotFound(new CustomerCreateFitnessBookingResponse
            {
                Success = false,
                Message = "Biznis nije pronađen."
            });
        }

        var session = await _db.FitnessSessions
            .Include(x => x.FitnessRoom)
            .Include(x => x.FitnessClassType)
            .Include(x => x.Bookings)
            .FirstOrDefaultAsync(x =>
                x.Id == request.FitnessSessionId &&
                x.BusinessId == businessId);

        if (session is null)
        {
            return NotFound(new CustomerCreateFitnessBookingResponse
            {
                Success = false,
                Message = "Termin nije pronađen."
            });
        }

        if (session.Status != FitnessSessionStatus.Scheduled)
        {
            return BadRequest(new CustomerCreateFitnessBookingResponse
            {
                Success = false,
                Message = "Prijava je moguća samo za zakazane termine."
            });
        }

        if (session.StartAtUtc <= DateTime.UtcNow)
        {
            return BadRequest(new CustomerCreateFitnessBookingResponse
            {
                Success = false,
                Message = "Nije moguće prijaviti se na termin koji je već počeo ili je prošao."
            });
        }

        var customerInfo = await ResolveCustomerInfo(request);

        if (customerInfo is null)
        {
            return BadRequest(new CustomerCreateFitnessBookingResponse
            {
                Success = false,
                Message = "Nije pronađen korisnik za prijavu na termin."
            });
        }

        var duplicate = await _db.FitnessSessionBookings
            .AnyAsync(x =>
                x.FitnessSessionId == request.FitnessSessionId &&
                x.Status != FitnessSessionBookingStatus.CancelledByBusiness &&
                x.Status != FitnessSessionBookingStatus.CancelledByCustomer &&
                x.Status != FitnessSessionBookingStatus.Rejected &&
                (
                    (customerInfo.CustomerProfileId != null && x.CustomerProfileId == customerInfo.CustomerProfileId) ||
                    (customerInfo.BusinessCustomerId != null && x.BusinessCustomerId == customerInfo.BusinessCustomerId) ||
                    (customerInfo.AppUserId != null && x.AppUserId == customerInfo.AppUserId) ||
                    x.CustomerPhone == customerInfo.CustomerPhone
                ));

        if (duplicate)
        {
            return BadRequest(new CustomerCreateFitnessBookingResponse
            {
                Success = false,
                Message = "Već ste prijavljeni na ovaj termin."
            });
        }

        var bookedCount = CountActiveBookings(session.Bookings);

        if (bookedCount >= session.Capacity)
        {
            return BadRequest(new CustomerCreateFitnessBookingResponse
            {
                Success = false,
                Message = "Termin je popunjen."
            });
        }

        var settings = await GetOrCreateSettings(businessId);

        var membershipStatus = await CheckMembershipStatus(
            businessId,
            customerInfo.CustomerProfileId,
            customerInfo.BusinessCustomerId,
            customerInfo.AppUserId,
            customerInfo.CustomerPhone);

        var bookingStatus = FitnessSessionBookingStatus.Booked;
        string? warningText = null;
        var message = "Uspešno ste prijavljeni na termin.";

        if (settings.MembershipsEnabled && !membershipStatus.IsActive)
        {
            warningText = "Klijent nije imao aktivnu članarinu u trenutku prijave.";

            if (settings.UnpaidMembershipBookingPolicy == FitnessUnpaidMembershipBookingPolicy.Block)
            {
                return BadRequest(new CustomerCreateFitnessBookingResponse
                {
                    Success = false,
                    Message = "Članarina nije aktivna. Prijava na termin nije moguća.",
                    MembershipWasActive = false,
                    MembershipWarningText = warningText
                });
            }

            if (settings.UnpaidMembershipBookingPolicy == FitnessUnpaidMembershipBookingPolicy.AllowWithNotification)
            {
                message = "Prijava je uspešna, ali nemate aktivnu članarinu. Molimo vas da regulišete članarinu pre dolaska.";
            }

            if (settings.UnpaidMembershipBookingPolicy == FitnessUnpaidMembershipBookingPolicy.RequireApproval)
            {
                bookingStatus = FitnessSessionBookingStatus.PendingApproval;
                message = "Zahtev za prijavu je poslat. Teretana treba da odobri prijavu.";
            }
        }

        var now = DateTime.UtcNow;

        var booking = new FitnessSessionBooking
        {
            BusinessId = businessId,
            FitnessSessionId = session.Id,
            CustomerProfileId = customerInfo.CustomerProfileId,
            BusinessCustomerId = customerInfo.BusinessCustomerId,
            AppUserId = customerInfo.AppUserId,
            CustomerName = customerInfo.CustomerName,
            CustomerPhone = customerInfo.CustomerPhone,
            Status = bookingStatus,
            MembershipWasActiveAtBooking = membershipStatus.IsActive,
            MembershipWarningText = membershipStatus.IsActive ? null : warningText,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _db.FitnessSessionBookings.Add(booking);
        await _db.SaveChangesAsync();

        return Ok(new CustomerCreateFitnessBookingResponse
        {
            Success = true,
            BookingId = booking.Id,
            BookingStatus = (int)booking.Status,
            BookingStatusText = GetBookingStatusText(booking.Status),
            Message = message,
            MembershipWasActive = membershipStatus.IsActive,
            MembershipWarningText = booking.MembershipWarningText
        });
    }

    // ============================================================
    // CUSTOMER CANCEL BOOKING / ODUSTAJANJE KLIJENTA
    // ============================================================

    [HttpPost("sessions/{sessionId:long}/cancel")]
    public async Task<ActionResult<CustomerCreateFitnessBookingResponse>> CancelBooking(
        long sessionId,
        CustomerCancelFitnessBookingRequest request)
    {
        if (sessionId <= 0)
        {
            return BadRequest(new CustomerCreateFitnessBookingResponse
            {
                Success = false,
                Message = "sessionId je obavezan."
            });
        }

        var session = await _db.FitnessSessions
            .Include(x => x.Bookings)
            .FirstOrDefaultAsync(x => x.Id == sessionId);

        if (session is null)
        {
            return NotFound(new CustomerCreateFitnessBookingResponse
            {
                Success = false,
                Message = "Termin nije pronađen."
            });
        }

        if (session.Status != FitnessSessionStatus.Scheduled)
        {
            return BadRequest(new CustomerCreateFitnessBookingResponse
            {
                Success = false,
                Message = "Odustajanje je moguće samo za zakazane termine."
            });
        }

        var now = DateTime.UtcNow;

        if (session.StartAtUtc <= now)
        {
            return BadRequest(new CustomerCreateFitnessBookingResponse
            {
                Success = false,
                Message = "Nije moguće odustati od termina koji je već počeo ili je prošao."
            });
        }

        var settings = await GetOrCreateSettings(session.BusinessId);

        if (!settings.AllowCustomerCancelBooking)
        {
            return BadRequest(new CustomerCreateFitnessBookingResponse
            {
                Success = false,
                Message = "Ova teretana ne dozvoljava odustajanje od termina preko aplikacije."
            });
        }

        var deadlineMinutes = settings.CustomerCancelDeadlineMinutes;

        if (deadlineMinutes > 0)
        {
            var latestCancelAtUtc = session.StartAtUtc.AddMinutes(-deadlineMinutes);

            if (now > latestCancelAtUtc)
            {
                return BadRequest(new CustomerCreateFitnessBookingResponse
                {
                    Success = false,
                    Message = $"Odustajanje je moguće najkasnije {deadlineMinutes} minuta pre početka termina."
                });
            }
        }

        var query = _db.FitnessSessionBookings
            .Where(x =>
                x.FitnessSessionId == sessionId &&
                (
                    x.Status == FitnessSessionBookingStatus.Booked ||
                    x.Status == FitnessSessionBookingStatus.PendingApproval
                ));

        if (request.CustomerProfileId is not null)
        {
            query = query.Where(x => x.CustomerProfileId == request.CustomerProfileId);
        }
        else if (request.BusinessCustomerId is not null)
        {
            query = query.Where(x => x.BusinessCustomerId == request.BusinessCustomerId);
        }
        else if (request.AppUserId is not null)
        {
            query = query.Where(x => x.AppUserId == request.AppUserId);
        }
        else if (!string.IsNullOrWhiteSpace(request.CustomerPhone))
        {
            var phone = request.CustomerPhone.Trim();
            query = query.Where(x => x.CustomerPhone == phone);
        }
        else
        {
            return BadRequest(new CustomerCreateFitnessBookingResponse
            {
                Success = false,
                Message = "Nedostaje klijent za odustajanje od termina."
            });
        }

        var booking = await query.FirstOrDefaultAsync();

        if (booking is null)
        {
            return NotFound(new CustomerCreateFitnessBookingResponse
            {
                Success = false,
                Message = "Vaša prijava za ovaj termin nije pronađena."
            });
        }

        booking.Status = FitnessSessionBookingStatus.CancelledByCustomer;
        booking.UpdatedAtUtc = now;

        await _db.SaveChangesAsync();

        return Ok(new CustomerCreateFitnessBookingResponse
        {
            Success = true,
            BookingId = booking.Id,
            BookingStatus = (int)booking.Status,
            BookingStatusText = GetBookingStatusText(booking.Status),
            Message = "Odustali ste od termina."
        });
    }

    // ============================================================
    // HELPERS
    // ============================================================

    private async Task<CustomerBookingInfo?> ResolveCustomerInfo(
        CustomerCreateFitnessBookingRequest request)
    {
        if (request.CustomerProfileId is not null)
        {
            var profile = await _db.CustomerProfiles
                .FirstOrDefaultAsync(x => x.Id == request.CustomerProfileId);

            if (profile is not null)
            {
                var name =
                    !string.IsNullOrWhiteSpace(request.CustomerName)
                        ? request.CustomerName.Trim()
                        : !string.IsNullOrWhiteSpace(profile.Nickname)
                            ? profile.Nickname.Trim()
                            : !string.IsNullOrWhiteSpace(profile.FullName)
                                ? profile.FullName.Trim()
                                : "Klijent";

                var phone =
                    !string.IsNullOrWhiteSpace(request.CustomerPhone)
                        ? request.CustomerPhone.Trim()
                        : !string.IsNullOrWhiteSpace(profile.Phone)
                            ? profile.Phone.Trim()
                            : string.Empty;

                if (string.IsNullOrWhiteSpace(phone))
                {
                    return null;
                }

                return new CustomerBookingInfo
                {
                    CustomerProfileId = profile.Id,
                    AppUserId = request.AppUserId,
                    BusinessCustomerId = request.BusinessCustomerId,
                    CustomerName = name,
                    CustomerPhone = phone
                };
            }
        }

        if (request.BusinessCustomerId is not null)
        {
            var customer = await _db.BusinessCustomers
                .FirstOrDefaultAsync(x => x.Id == request.BusinessCustomerId);

            if (customer is not null)
            {
                var name =
                    !string.IsNullOrWhiteSpace(request.CustomerName)
                        ? request.CustomerName.Trim()
                        : !string.IsNullOrWhiteSpace(customer.FullName)
                            ? customer.FullName.Trim()
                            : "Klijent";

                var phone =
                    !string.IsNullOrWhiteSpace(request.CustomerPhone)
                        ? request.CustomerPhone.Trim()
                        : !string.IsNullOrWhiteSpace(customer.Phone)
                            ? customer.Phone.Trim()
                            : string.Empty;

                if (string.IsNullOrWhiteSpace(phone))
                {
                    return null;
                }

                return new CustomerBookingInfo
                {
                    CustomerProfileId = request.CustomerProfileId,
                    AppUserId = request.AppUserId,
                    BusinessCustomerId = customer.Id,
                    CustomerName = name,
                    CustomerPhone = phone
                };
            }
        }

        if (!string.IsNullOrWhiteSpace(request.CustomerName) &&
            !string.IsNullOrWhiteSpace(request.CustomerPhone))
        {
            return new CustomerBookingInfo
            {
                CustomerProfileId = request.CustomerProfileId,
                AppUserId = request.AppUserId,
                BusinessCustomerId = request.BusinessCustomerId,
                CustomerName = request.CustomerName.Trim(),
                CustomerPhone = request.CustomerPhone.Trim()
            };
        }

        return null;
    }

    private async Task<FitnessSettings> GetOrCreateSettings(long businessId)
    {
        var settings = await _db.FitnessSettings
            .FirstOrDefaultAsync(x => x.BusinessId == businessId);

        if (settings is not null)
        {
            return settings;
        }

        var now = DateTime.UtcNow;

        settings = new FitnessSettings
        {
            BusinessId = businessId,
            GroupClassesEnabled = true,
            IndividualTrainingEnabled = false,
            MembershipsEnabled = true,
            UnpaidMembershipBookingPolicy = FitnessUnpaidMembershipBookingPolicy.AllowWithNotification,
            DefaultMembershipDurationDays = 30,
            AllowCustomerCancelBooking = true,
            CustomerCancelDeadlineMinutes = 120,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _db.FitnessSettings.Add(settings);
        await _db.SaveChangesAsync();

        return settings;
    }

    private async Task<(bool IsActive, DateOnly? ActiveUntil)> CheckMembershipStatus(
        long businessId,
        long? customerProfileId,
        long? businessCustomerId,
        long? appUserId,
        string? customerPhone)
    {
        var memberQuery = _db.FitnessMembers
            .Include(x => x.Payments)
            .Where(x =>
                x.BusinessId == businessId &&
                x.IsActive);

        if (customerProfileId is not null)
        {
            memberQuery = memberQuery.Where(x => x.CustomerProfileId == customerProfileId);
        }
        else if (businessCustomerId is not null)
        {
            memberQuery = memberQuery.Where(x => x.BusinessCustomerId == businessCustomerId);
        }
        else if (appUserId is not null)
        {
            memberQuery = memberQuery.Where(x => x.AppUserId == appUserId);
        }
        else if (!string.IsNullOrWhiteSpace(customerPhone))
        {
            var phone = customerPhone.Trim();
            memberQuery = memberQuery.Where(x => x.Phone == phone);
        }
        else
        {
            return (false, null);
        }

        var member = await memberQuery.FirstOrDefaultAsync();

        if (member is null)
        {
            return (false, null);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var activePayment = member.Payments
            .Where(x => x.PeriodStartDate <= today && x.PeriodEndDate >= today)
            .OrderByDescending(x => x.PeriodEndDate)
            .FirstOrDefault();

        return activePayment is null
            ? (false, null)
            : (true, activePayment.PeriodEndDate);
    }

    private static CustomerFitnessSessionDto ToCustomerSessionDto(
        FitnessSession session,
        string businessName,
        long? appUserId,
        long? customerProfileId)
    {
        var bookedCount = CountActiveBookings(session.Bookings);

        var isAlreadyBooked = session.Bookings.Any(x =>
            x.Status != FitnessSessionBookingStatus.CancelledByBusiness &&
            x.Status != FitnessSessionBookingStatus.CancelledByCustomer &&
            x.Status != FitnessSessionBookingStatus.Rejected &&
            (
                (appUserId != null && x.AppUserId == appUserId) ||
                (customerProfileId != null && x.CustomerProfileId == customerProfileId)
            ));

        return new CustomerFitnessSessionDto
        {
            Id = session.Id,
            BusinessId = session.BusinessId,
            BusinessName = businessName,
            FitnessRoomId = session.FitnessRoomId,
            FitnessRoomName = session.FitnessRoom?.Name ?? string.Empty,
            FitnessClassTypeId = session.FitnessClassTypeId,
            FitnessClassTypeName = session.FitnessClassType?.Name,
            TrainerName = null,
            SessionType = (int)session.SessionType,
            SessionTypeText = GetSessionTypeText(session.SessionType),
            StartAtUtc = session.StartAtUtc,
            EndAtUtc = session.EndAtUtc,
            Capacity = session.Capacity,
            BookedCount = bookedCount,
            CapacityText = session.SessionType == FitnessSessionType.Individual
                ? bookedCount > 0 ? "zauzeto" : "slobodno"
                : $"{bookedCount}/{session.Capacity}",
            IsFull = bookedCount >= session.Capacity,
            CanBook =
                session.Status == FitnessSessionStatus.Scheduled &&
                session.StartAtUtc > DateTime.UtcNow &&
                bookedCount < session.Capacity &&
                !isAlreadyBooked,
            IsAlreadyBookedByCurrentCustomer = isAlreadyBooked,
            Note = session.Note
        };
    }

    private static string BuildCustomerPublicDisplayName(string? customerName)
    {
        if (string.IsNullOrWhiteSpace(customerName))
        {
            return "Gost";
        }

        var normalized = customerName.Trim();

        if (normalized.Length <= 1)
        {
            return normalized;
        }

        var parts = normalized
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        if (parts.Count == 0)
        {
            return "Gost";
        }

        if (parts.Count == 1)
        {
            return parts[0];
        }

        var firstName = parts[0];
        var lastNameInitial = parts[1][0];

        return $"{firstName} {lastNameInitial}.";
    }

    private static int CountActiveBookings(IEnumerable<FitnessSessionBooking> bookings)
    {
        return bookings.Count(x =>
            x.Status == FitnessSessionBookingStatus.Booked ||
            x.Status == FitnessSessionBookingStatus.Attended ||
            x.Status == FitnessSessionBookingStatus.PendingApproval);
    }

    private static string GetSessionTypeText(FitnessSessionType type)
    {
        return type switch
        {
            FitnessSessionType.Group => "Grupni trening",
            FitnessSessionType.Individual => "Individualni trening",
            _ => "Nepoznato"
        };
    }

    private static string GetBookingStatusText(FitnessSessionBookingStatus status)
    {
        return status switch
        {
            FitnessSessionBookingStatus.PendingApproval => "Čeka odobrenje",
            FitnessSessionBookingStatus.Booked => "Prijavljen",
            FitnessSessionBookingStatus.Rejected => "Odbijen",
            FitnessSessionBookingStatus.CancelledByCustomer => "Otkazao klijent",
            FitnessSessionBookingStatus.CancelledByBusiness => "Otkazala teretana",
            FitnessSessionBookingStatus.Attended => "Došao",
            FitnessSessionBookingStatus.NoShow => "Nije došao",
            _ => "Nepoznato"
        };
    }

    private sealed class CustomerBookingInfo
    {
        public long? CustomerProfileId { get; set; }

        public long? BusinessCustomerId { get; set; }

        public long? AppUserId { get; set; }

        public string CustomerName { get; set; } = string.Empty;

        public string CustomerPhone { get; set; } = string.Empty;
    }
}