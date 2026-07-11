using BookingPlatform.Contracts.Fitness;
using BookingPlatform.Domain.BusinessActivityNotifications;
using BookingPlatform.Domain.Fitness;
using BookingPlatform.Domain.Scheduling;
using BookingPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using BookingPlatform.Domain.Businesses;


namespace BookingPlatform.Api.Controllers;

[ApiController]
[Route("api/fitness")]
public sealed class FitnessController : ControllerBase
{
    private readonly BookingDbContext _db;

    public FitnessController(BookingDbContext db)
    {
        _db = db;
    }

    // ============================================================
    // MEMBER SESSION DEBTS / ZADUŽENI TERMINI
    // ============================================================

    [HttpGet("businesses/{businessId:long}/session-debts")]
    public async Task<ActionResult<List<FitnessMemberSessionDebtDto>>> GetSessionDebts(
        long businessId,
        [FromQuery] long? memberId,
        [FromQuery] int? status)
    {
        var query = _db.FitnessMemberSessionDebts
            .AsNoTracking()
            .Include(x => x.FitnessMember)
            .Include(x => x.FitnessSession)
                .ThenInclude(x => x.FitnessRoom)
            .Include(x => x.FitnessClassType)
            .Include(x => x.FitnessMemberTrainingPass)
            .Where(x => x.BusinessId == businessId);

        if (memberId.HasValue)
        {
            query = query.Where(x => x.FitnessMemberId == memberId.Value);
        }

        if (status.HasValue)
        {
            if (!Enum.IsDefined(typeof(FitnessMemberSessionDebtStatus), status.Value))
            {
                return BadRequest("Nepoznat status zaduženog termina.");
            }

            var parsedStatus = (FitnessMemberSessionDebtStatus)status.Value;
            query = query.Where(x => x.Status == parsedStatus);
        }

        var debts = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync();

        return Ok(debts.Select(ToSessionDebtDto).ToList());
    }

    [HttpPost("businesses/{businessId:long}/session-debts/{debtId:long}/void")]
    public async Task<ActionResult<FitnessMemberSessionDebtDto>> VoidSessionDebt(
        long businessId,
        long debtId,
        VoidFitnessMemberSessionDebtRequest request)
    {
        var debt = await _db.FitnessMemberSessionDebts
            .Include(x => x.FitnessMember)
            .Include(x => x.FitnessSession)
                .ThenInclude(x => x.FitnessRoom)
            .Include(x => x.FitnessClassType)
            .Include(x => x.FitnessMemberTrainingPass)
            .FirstOrDefaultAsync(x =>
                x.Id == debtId &&
                x.BusinessId == businessId);

        if (debt is null)
        {
            return NotFound("Zaduženi termin nije pronađen.");
        }

        if (debt.Status == FitnessMemberSessionDebtStatus.Settled)
        {
            return BadRequest("Nije moguće stornirati zaduženi termin koji je već prebijen uplatom.");
        }

        if (debt.Status == FitnessMemberSessionDebtStatus.Voided)
        {
            return BadRequest("Zaduženi termin je već storniran.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return BadRequest("Unesite razlog storniranja.");
        }

        var nowUtc = DateTime.UtcNow;

        debt.Status = FitnessMemberSessionDebtStatus.Voided;
        debt.VoidedAtUtc = nowUtc;
        debt.VoidReason = request.Reason.Trim();
        debt.UpdatedAtUtc = nowUtc;

        await ResolveBusinessNotificationByKeyAsync(
            businessId,
            FitnessOpenDebtNotificationKey(debt.Id),
            nowUtc);

        await _db.SaveChangesAsync();

        return Ok(ToSessionDebtDto(debt));
    }

    // ============================================================
    // ROOMS / SALE
    // ============================================================

    [HttpGet("businesses/{businessId:long}/rooms")]
    public async Task<ActionResult<List<FitnessRoomDto>>> GetRooms(long businessId)
    {
        var rooms = await _db.FitnessRooms
            .Where(x => x.BusinessId == businessId)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Name)
            .Select(x => ToRoomDto(x))
            .ToListAsync();

        return Ok(rooms);
    }

    [HttpPost("businesses/{businessId:long}/rooms")]
    public async Task<ActionResult<FitnessRoomDto>> CreateRoom(
        long businessId,
        CreateFitnessRoomRequest request)
    {
        if (!await BusinessExists(businessId))
        {
            return NotFound("Biznis nije pronađen.");
        }

        var validationError = ValidateRoomRequest(request.Name, request.Capacity);
        if (validationError is not null)
        {
            return BadRequest(validationError);
        }

        var name = request.Name.Trim();

        var exists = await _db.FitnessRooms
            .AnyAsync(x => x.BusinessId == businessId && x.Name.ToLower() == name.ToLower());

        if (exists)
        {
            return BadRequest("Sala sa tim nazivom već postoji.");
        }

        var now = DateTime.UtcNow;

        var room = new FitnessRoom
        {
            BusinessId = businessId,
            Name = name,
            Capacity = request.Capacity,
            IsActive = request.IsActive,
            AllowsGroupClasses = request.AllowsGroupClasses,
            AllowsIndividualTraining = request.AllowsIndividualTraining,
            DisplayOrder = request.DisplayOrder,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _db.FitnessRooms.Add(room);
        await _db.SaveChangesAsync();

        return Ok(ToRoomDto(room));
    }

    [HttpPut("businesses/{businessId:long}/rooms/{roomId:long}")]
    public async Task<ActionResult<FitnessRoomDto>> UpdateRoom(
        long businessId,
        long roomId,
        UpdateFitnessRoomRequest request)
    {
        var room = await _db.FitnessRooms
            .FirstOrDefaultAsync(x => x.Id == roomId && x.BusinessId == businessId);

        if (room is null)
        {
            return NotFound("Sala nije pronađena.");
        }

        var validationError = ValidateRoomRequest(request.Name, request.Capacity);
        if (validationError is not null)
        {
            return BadRequest(validationError);
        }

        var name = request.Name.Trim();

        var exists = await _db.FitnessRooms
            .AnyAsync(x =>
                x.BusinessId == businessId &&
                x.Id != roomId &&
                x.Name.ToLower() == name.ToLower());

        if (exists)
        {
            return BadRequest("Sala sa tim nazivom već postoji.");
        }

        room.Name = name;
        room.Capacity = request.Capacity;
        room.IsActive = request.IsActive;
        room.AllowsGroupClasses = request.AllowsGroupClasses;
        room.AllowsIndividualTraining = request.AllowsIndividualTraining;
        room.DisplayOrder = request.DisplayOrder;
        room.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(ToRoomDto(room));
    }

    // ============================================================
    // CLASS TYPES / TIPOVI TRENINGA
    // ============================================================

    [HttpGet("businesses/{businessId:long}/class-types")]
    public async Task<ActionResult<List<FitnessClassTypeDto>>> GetClassTypes(long businessId)
    {
        var items = await _db.FitnessClassTypes
            .Where(x => x.BusinessId == businessId)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Name)
            .Select(x => ToClassTypeDto(x))
            .ToListAsync();

        return Ok(items);
    }

    [HttpPost("businesses/{businessId:long}/class-types")]
    public async Task<ActionResult<FitnessClassTypeDto>> CreateClassType(
        long businessId,
        CreateFitnessClassTypeRequest request)
    {
        if (!await BusinessExists(businessId))
        {
            return NotFound("Biznis nije pronađen.");
        }

        var validationError = ValidateClassTypeRequest(
            request.Name,
            request.DefaultDurationMin,
            request.DefaultCapacity);

        if (validationError is not null)
        {
            return BadRequest(validationError);
        }

        var name = request.Name.Trim();

        var exists = await _db.FitnessClassTypes
            .AnyAsync(x => x.BusinessId == businessId && x.Name.ToLower() == name.ToLower());

        if (exists)
        {
            return BadRequest("Tip treninga sa tim nazivom već postoji.");
        }

        var now = DateTime.UtcNow;

        var item = new FitnessClassType
        {
            BusinessId = businessId,
            Name = name,
            Description = NormalizeNullableText(request.Description),
            DefaultDurationMin = request.DefaultDurationMin,
            DefaultCapacity = request.DefaultCapacity,
            IsActive = request.IsActive,
            DisplayOrder = request.DisplayOrder,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _db.FitnessClassTypes.Add(item);
        await _db.SaveChangesAsync();

        return Ok(ToClassTypeDto(item));
    }

    [HttpPut("businesses/{businessId:long}/class-types/{classTypeId:long}")]
    public async Task<ActionResult<FitnessClassTypeDto>> UpdateClassType(
        long businessId,
        long classTypeId,
        UpdateFitnessClassTypeRequest request)
    {
        var item = await _db.FitnessClassTypes
            .FirstOrDefaultAsync(x => x.Id == classTypeId && x.BusinessId == businessId);

        if (item is null)
        {
            return NotFound("Tip treninga nije pronađen.");
        }

        var validationError = ValidateClassTypeRequest(
            request.Name,
            request.DefaultDurationMin,
            request.DefaultCapacity);

        if (validationError is not null)
        {
            return BadRequest(validationError);
        }

        var name = request.Name.Trim();

        var exists = await _db.FitnessClassTypes
            .AnyAsync(x =>
                x.BusinessId == businessId &&
                x.Id != classTypeId &&
                x.Name.ToLower() == name.ToLower());

        if (exists)
        {
            return BadRequest("Tip treninga sa tim nazivom već postoji.");
        }

        item.Name = name;
        item.Description = NormalizeNullableText(request.Description);
        item.DefaultDurationMin = request.DefaultDurationMin;
        item.DefaultCapacity = request.DefaultCapacity;
        item.IsActive = request.IsActive;
        item.DisplayOrder = request.DisplayOrder;
        item.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(ToClassTypeDto(item));
    }

    // ============================================================
    // SETTINGS / PODEŠAVANJA
    // ============================================================

    [HttpGet("businesses/{businessId:long}/settings")]
    public async Task<ActionResult<FitnessSettingsDto>> GetSettings(long businessId)
    {
        if (!await BusinessExists(businessId))
        {
            return NotFound("Biznis nije pronađen.");
        }

        var settings = await _db.FitnessSettings
            .FirstOrDefaultAsync(x => x.BusinessId == businessId);

        if (settings is null)
        {
            settings = CreateDefaultSettings(businessId);
            _db.FitnessSettings.Add(settings);
            await _db.SaveChangesAsync();
        }

        return Ok(ToSettingsDto(settings));
    }

    [HttpPut("businesses/{businessId:long}/settings")]
    public async Task<ActionResult<FitnessSettingsDto>> UpdateSettings(
        long businessId,
        UpdateFitnessSettingsRequest request)
    {
        if (!await BusinessExists(businessId))
        {
            return NotFound("Biznis nije pronađen.");
        }

        if (!Enum.IsDefined(typeof(FitnessUnpaidMembershipBookingPolicy), request.UnpaidMembershipBookingPolicy))
        {
            return BadRequest("Nepoznata politika za neplaćenu članarinu.");
        }

        if (request.DefaultMembershipDurationDays <= 0)
        {
            return BadRequest("Podrazumevano trajanje članarine mora biti veće od 0 dana.");
        }

        if (request.CustomerCancelDeadlineMinutes < 0)
        {
            return BadRequest("Rok za otkazivanje ne može biti negativan.");
        }

        var settings = await _db.FitnessSettings
            .FirstOrDefaultAsync(x => x.BusinessId == businessId);

        if (settings is null)
        {
            settings = CreateDefaultSettings(businessId);
            _db.FitnessSettings.Add(settings);
        }

        settings.GroupClassesEnabled = request.GroupClassesEnabled;
        settings.IndividualTrainingEnabled = request.IndividualTrainingEnabled;
        settings.ReceivesCustomerMessages = request.ReceivesCustomerMessages;
        settings.MembershipsEnabled = request.MembershipsEnabled;
        settings.UnpaidMembershipBookingPolicy =
            (FitnessUnpaidMembershipBookingPolicy)request.UnpaidMembershipBookingPolicy;
        settings.DefaultMembershipDurationDays = request.DefaultMembershipDurationDays;
        settings.AllowCustomerCancelBooking = request.AllowCustomerCancelBooking;
        settings.CustomerCancelDeadlineMinutes = request.CustomerCancelDeadlineMinutes;
        settings.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(ToSettingsDto(settings));
    }

    // ============================================================
    // SESSIONS / TERMINI
    // ============================================================

    [HttpGet("businesses/{businessId:long}/sessions")]
    public async Task<ActionResult<List<FitnessSessionDto>>> GetSessions(
        long businessId,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc)
    {
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
                x.StartAtUtc >= from &&
                x.StartAtUtc < to)
            .OrderBy(x => x.StartAtUtc)
            .ThenBy(x => x.FitnessRoom.Name)
            .ToListAsync();

        return Ok(sessions.Select(ToSessionDto).ToList());
    }

    [HttpPost("businesses/{businessId:long}/sessions")]
    public async Task<ActionResult<FitnessSessionDto>> CreateSession(
        long businessId,
        CreateFitnessSessionRequest request)
    {
        if (!await BusinessExists(businessId))
        {
            return NotFound("Biznis nije pronađen.");
        }

        var validationResult = await ValidateSessionRequest(
            businessId,
            sessionId: null,
            request.FitnessRoomId,
            request.FitnessClassTypeId,
            request.TrainerStaffMemberId,
            request.SessionType,
            request.StartAtUtc,
            request.EndAtUtc,
            request.Capacity);

        if (validationResult is not null)
        {
            return BadRequest(validationResult);
        }

        var now = DateTime.UtcNow;

        var session = new FitnessSession
        {
            BusinessId = businessId,
            FitnessRoomId = request.FitnessRoomId,
            FitnessClassTypeId = request.FitnessClassTypeId,
            TrainerStaffMemberId = request.TrainerStaffMemberId,
            SessionType = (FitnessSessionType)request.SessionType,
            StartAtUtc = request.StartAtUtc,
            EndAtUtc = request.EndAtUtc,
            Capacity = request.Capacity,
            Status = FitnessSessionStatus.Scheduled,
            Note = NormalizeNullableText(request.Note),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _db.FitnessSessions.Add(session);
        await _db.SaveChangesAsync();

        session = await LoadSession(session.Id);

        return Ok(ToSessionDto(session!));
    }

    [HttpPut("businesses/{businessId:long}/sessions/{sessionId:long}")]
    public async Task<ActionResult<FitnessSessionDto>> UpdateSession(
        long businessId,
        long sessionId,
        UpdateFitnessSessionRequest request)
    {
        var session = await _db.FitnessSessions
            .Include(x => x.Bookings)
            .FirstOrDefaultAsync(x => x.Id == sessionId && x.BusinessId == businessId);

        if (session is null)
        {
            return NotFound("Termin nije pronađen.");
        }

        if (!Enum.IsDefined(typeof(FitnessSessionStatus), request.Status))
        {
            return BadRequest("Nepoznat status termina.");
        }

        var validationResult = await ValidateSessionRequest(
            businessId,
            sessionId,
            request.FitnessRoomId,
            request.FitnessClassTypeId,
            request.TrainerStaffMemberId,
            request.SessionType,
            request.StartAtUtc,
            request.EndAtUtc,
            request.Capacity);

        if (validationResult is not null)
        {
            return BadRequest(validationResult);
        }

        var activeBookedCount = CountActiveBookings(session.Bookings);

        if (request.Capacity < activeBookedCount)
        {
            return BadRequest($"Kapacitet ne može biti manji od broja prijavljenih klijenata ({activeBookedCount}).");
        }

        session.FitnessRoomId = request.FitnessRoomId;
        session.FitnessClassTypeId = request.FitnessClassTypeId;
        session.TrainerStaffMemberId = request.TrainerStaffMemberId;
        session.SessionType = (FitnessSessionType)request.SessionType;
        session.StartAtUtc = request.StartAtUtc;
        session.EndAtUtc = request.EndAtUtc;
        session.Capacity = request.Capacity;
        session.Status = (FitnessSessionStatus)request.Status;
        session.Note = NormalizeNullableText(request.Note);
        session.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        session = await LoadSession(session.Id);

        return Ok(ToSessionDto(session!));
    }

    // ============================================================
    // BOOKINGS / PRIJAVE NA TERMIN
    // ============================================================

    [HttpGet("sessions/{sessionId:long}/bookings")]
    public async Task<ActionResult<List<FitnessSessionBookingDto>>> GetSessionBookings(long sessionId)
    {
        var exists = await _db.FitnessSessions.AnyAsync(x => x.Id == sessionId);
        if (!exists)
        {
            return NotFound("Termin nije pronađen.");
        }

        var bookings = await _db.FitnessSessionBookings
            .Where(x => x.FitnessSessionId == sessionId)
            .OrderBy(x => x.CreatedAtUtc)
            .Select(x => ToBookingDto(x))
            .ToListAsync();

        return Ok(bookings);
    }

    [HttpPost("businesses/{businessId:long}/bookings")]
    public async Task<ActionResult<FitnessSessionBookingDto>> CreateBooking(
        long businessId,
        CreateFitnessSessionBookingRequest request)
    {
        var session = await _db.FitnessSessions
            .Include(x => x.Bookings)
            .Include(x => x.FitnessRoom)
            .Include(x => x.FitnessClassType)
            .FirstOrDefaultAsync(x =>
                x.Id == request.FitnessSessionId &&
                x.BusinessId == businessId);

        if (session is null)
        {
            return NotFound("Termin nije pronađen.");
        }

        if (session.Status != FitnessSessionStatus.Scheduled)
        {
            return BadRequest("Prijava je moguća samo za zakazane termine.");
        }

        if (session.StartAtUtc <= DateTime.UtcNow)
        {
            return BadRequest("Nije moguće prijaviti klijenta na termin koji je već počeo ili je prošao.");
        }

        if (string.IsNullOrWhiteSpace(request.CustomerName))
        {
            return BadRequest("Ime klijenta je obavezno.");
        }

        if (string.IsNullOrWhiteSpace(request.CustomerPhone))
        {
            request.CustomerPhone = "-";
        }

        var duplicate = await _db.FitnessSessionBookings
            .AnyAsync(x =>
                x.FitnessSessionId == request.FitnessSessionId &&
                x.Status != FitnessSessionBookingStatus.CancelledByBusiness &&
                x.Status != FitnessSessionBookingStatus.CancelledByCustomer &&
                x.Status != FitnessSessionBookingStatus.Rejected &&
                (
                    (request.CustomerProfileId != null && x.CustomerProfileId == request.CustomerProfileId) ||
                    (request.BusinessCustomerId != null && x.BusinessCustomerId == request.BusinessCustomerId) ||
                    (request.AppUserId != null && x.AppUserId == request.AppUserId) ||
                    x.CustomerPhone == request.CustomerPhone.Trim()
                ));

        if (duplicate)
        {
            return BadRequest("Klijent je već prijavljen na ovaj termin.");
        }

        var activeBookedCount = CountActiveBookings(session.Bookings);

        if (activeBookedCount >= session.Capacity)
        {
            return BadRequest("Termin je popunjen.");
        }

        var settings = await GetOrCreateSettings(businessId);

        var bookingStatus = FitnessSessionBookingStatus.Booked;
        var membershipWasActive = true;
        string? warningText = null;
        FitnessMemberTrainingPass? selectedTrainingPass = null;

        if (settings.MembershipsEnabled)
        {
            var trainingPassResult = await FindUsableTrainingPassForBooking(
                businessId,
                request.FitnessMemberId,
                request.CustomerProfileId,
                request.BusinessCustomerId,
                request.AppUserId,
                request.CustomerPhone,
                session,
                DateTime.UtcNow);

            membershipWasActive = trainingPassResult.CanUse;
            selectedTrainingPass = trainingPassResult.TrainingPass;

            if (!trainingPassResult.CanUse)
            {
                warningText = trainingPassResult.Message;

                if (settings.UnpaidMembershipBookingPolicy == FitnessUnpaidMembershipBookingPolicy.Block)
                {
                    return BadRequest(trainingPassResult.Message);
                }

                if (settings.UnpaidMembershipBookingPolicy == FitnessUnpaidMembershipBookingPolicy.RequireApproval)
                {
                    bookingStatus = FitnessSessionBookingStatus.PendingApproval;
                }
            }
        }

        var now = DateTime.UtcNow;

        FitnessMemberSessionDebt? createdOpenDebt = null;

        var booking = new FitnessSessionBooking
        {
            BusinessId = businessId,
            FitnessSessionId = request.FitnessSessionId,
            FitnessMemberId = request.FitnessMemberId,
            CustomerProfileId = request.CustomerProfileId,
            BusinessCustomerId = request.BusinessCustomerId,
            AppUserId = request.AppUserId,
            CustomerName = request.CustomerName.Trim(),
            CustomerPhone = request.CustomerPhone.Trim(),
            Status = bookingStatus,
            MembershipWasActiveAtBooking = membershipWasActive,
            MembershipWarningText = membershipWasActive ? null : warningText,
            FitnessMemberTrainingPassId = selectedTrainingPass?.Id,
            ConsumesTrainingPassSession = selectedTrainingPass is not null,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _db.FitnessSessionBookings.Add(booking);

        if (settings.MembershipsEnabled &&
            booking.Status == FitnessSessionBookingStatus.Booked &&
            selectedTrainingPass is null &&
            request.FitnessMemberId.HasValue)
        {
            var existingOpenDebt = await _db.FitnessMemberSessionDebts
                .AnyAsync(x =>
                    x.BusinessId == businessId &&
                    x.FitnessMemberId == request.FitnessMemberId.Value &&
                    x.FitnessSessionId == session.Id &&
                    x.Status == FitnessMemberSessionDebtStatus.Open);

            if (!existingOpenDebt)
            {
                var nowForDebt = DateTime.UtcNow;

                createdOpenDebt = new FitnessMemberSessionDebt
                {
                    BusinessId = businessId,
                    FitnessMemberId = request.FitnessMemberId.Value,
                    FitnessSessionId = session.Id,
                    FitnessClassTypeId = session.FitnessClassTypeId,
                    SessionsCount = 1,
                    Status = FitnessMemberSessionDebtStatus.Open,
                    Note = "Automatski zadužen termin jer član nije imao aktivan paket u trenutku prijave.",
                    CreatedAtUtc = nowForDebt,
                    UpdatedAtUtc = nowForDebt
                };

                _db.FitnessMemberSessionDebts.Add(createdOpenDebt);
            }
        }

        await _db.SaveChangesAsync();

        await UpsertFitnessNotificationsForBookingAsync(
            session,
            booking,
            createdOpenDebt,
            warningText);

        await _db.SaveChangesAsync();

        return Ok(ToBookingDto(booking));
    }

    [HttpPut("bookings/{bookingId:long}/status")]
    public async Task<ActionResult<FitnessSessionBookingDto>> UpdateBookingStatus(
      long bookingId,
      UpdateFitnessSessionBookingStatusRequest request)
    {
        var booking = await _db.FitnessSessionBookings
            .FirstOrDefaultAsync(x => x.Id == bookingId);

        if (booking is null)
        {
            return NotFound("Prijava nije pronađena.");
        }

        if (!Enum.IsDefined(typeof(FitnessSessionBookingStatus), request.Status))
        {
            return BadRequest("Nepoznat status prijave.");
        }

        var newStatus = (FitnessSessionBookingStatus)request.Status;
        var now = DateTime.UtcNow;

        booking.Status = newStatus;
        booking.UpdatedAtUtc = now;

        if (newStatus != FitnessSessionBookingStatus.PendingApproval)
        {
            await ResolveBusinessNotificationByKeyAsync(
                booking.BusinessId,
                FitnessPendingBookingNotificationKey(booking.Id),
                now);
        }

        if (newStatus == FitnessSessionBookingStatus.Booked &&
            !booking.MembershipWasActiveAtBooking &&
            booking.FitnessMemberTrainingPassId is null &&
            booking.FitnessMemberId.HasValue)
        {
            var existingOpenDebt = await _db.FitnessMemberSessionDebts
                .FirstOrDefaultAsync(x =>
                    x.BusinessId == booking.BusinessId &&
                    x.FitnessMemberId == booking.FitnessMemberId.Value &&
                    x.FitnessSessionId == booking.FitnessSessionId &&
                    x.Status == FitnessMemberSessionDebtStatus.Open);

            if (existingOpenDebt is null)
            {
                var session = await _db.FitnessSessions
                    .Include(x => x.FitnessRoom)
                    .Include(x => x.FitnessClassType)
                    .FirstOrDefaultAsync(x =>
                        x.Id == booking.FitnessSessionId &&
                        x.BusinessId == booking.BusinessId);

                if (session is null)
                {
                    return NotFound("Termin nije pronađen.");
                }

                var createdOpenDebt = new FitnessMemberSessionDebt
                {
                    BusinessId = booking.BusinessId,
                    FitnessMemberId = booking.FitnessMemberId.Value,
                    FitnessSessionId = booking.FitnessSessionId,
                    FitnessClassTypeId = session.FitnessClassTypeId,
                    SessionsCount = 1,
                    Status = FitnessMemberSessionDebtStatus.Open,
                    Note = "Automatski zadužen termin jer je prijava bez aktivnog paketa odobrena.",
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                };

                _db.FitnessMemberSessionDebts.Add(createdOpenDebt);

                await _db.SaveChangesAsync();

                await UpsertBusinessNotificationAsync(new BusinessActivityNotification
                {
                    BusinessId = booking.BusinessId,
                    RecipientType = BusinessActivityNotificationRecipients.Business,
                    RecipientKey = "business",
                    Domain = BusinessActivityNotificationDomains.Fitness,
                    Kind = BusinessActivityNotificationKinds.OpenDebt,
                    ActivityKey = FitnessOpenDebtNotificationKey(createdOpenDebt.Id),
                    Title = "Otvoren zaduženi termin",
                    MainText = BuildFitnessNotificationSessionText(session),
                    PreviewText =
                        $"{BuildFitnessNotificationCustomerText(booking.CustomerName, booking.CustomerPhone)} | " +
                        "Prijava je odobrena bez aktivnog paketa. Termin je evidentiran kao zaduženje.",
                    Priority = 110,
                    SortAtUtc = booking.CreatedAtUtc,
                    FitnessSessionId = booking.FitnessSessionId,
                    FitnessSessionBookingId = booking.Id,
                    FitnessMemberId = booking.FitnessMemberId,
                    FitnessMemberSessionDebtId = createdOpenDebt.Id,
                    CustomerProfileId = booking.CustomerProfileId,
                    BusinessCustomerId = booking.BusinessCustomerId,
                    CustomerName = booking.CustomerName,
                    CustomerPhone = booking.CustomerPhone,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                });
            }

            await ResolveBusinessNotificationByKeyAsync(
                booking.BusinessId,
                FitnessMembershipWarningNotificationKey(booking.Id),
                now);
        }

        if (newStatus == FitnessSessionBookingStatus.CancelledByBusiness ||
            newStatus == FitnessSessionBookingStatus.CancelledByCustomer ||
            newStatus == FitnessSessionBookingStatus.Rejected)
        {
            booking.CancelledAtUtc = now;

            if (booking.FitnessMemberId.HasValue)
            {
                var openDebt = await _db.FitnessMemberSessionDebts
                    .FirstOrDefaultAsync(x =>
                        x.BusinessId == booking.BusinessId &&
                        x.FitnessMemberId == booking.FitnessMemberId.Value &&
                        x.FitnessSessionId == booking.FitnessSessionId &&
                        x.Status == FitnessMemberSessionDebtStatus.Open);

                if (openDebt is not null)
                {
                    openDebt.Status = FitnessMemberSessionDebtStatus.Voided;
                    openDebt.VoidedAtUtc = now;
                    openDebt.VoidReason = "Automatski stornirano jer je prijava otkazana ili odbijena.";
                    openDebt.UpdatedAtUtc = now;

                    await ResolveBusinessNotificationByKeyAsync(
                        booking.BusinessId,
                        FitnessOpenDebtNotificationKey(openDebt.Id),
                        now);
                }
            }

            await ResolveBusinessNotificationByKeyAsync(
                booking.BusinessId,
                FitnessMembershipWarningNotificationKey(booking.Id),
                now);
        }

        if (newStatus == FitnessSessionBookingStatus.Attended)
        {
            booking.AttendedAtUtc = now;
        }

        if (newStatus == FitnessSessionBookingStatus.NoShow)
        {
            booking.NoShowAtUtc = now;
        }

        await _db.SaveChangesAsync();

        return Ok(ToBookingDto(booking));
    }

    [HttpPost("bookings/{bookingId:long}/approve")]
    public async Task<ActionResult<FitnessSessionBookingDto>> ApproveBooking(long bookingId)
    {
        var booking = await _db.FitnessSessionBookings
            .FirstOrDefaultAsync(x => x.Id == bookingId);

        if (booking is null)
        {
            return NotFound("Prijava nije pronađena.");
        }

        if (booking.Status != FitnessSessionBookingStatus.PendingApproval)
        {
            return BadRequest("Odobravanje je moguće samo za prijavu koja čeka odobrenje.");
        }

        return await UpdateBookingStatus(
            bookingId,
            new UpdateFitnessSessionBookingStatusRequest
            {
                Status = (int)FitnessSessionBookingStatus.Booked
            });
    }

    [HttpPost("bookings/{bookingId:long}/reject")]
    public async Task<ActionResult<FitnessSessionBookingDto>> RejectBooking(long bookingId)
    {
        var booking = await _db.FitnessSessionBookings
            .FirstOrDefaultAsync(x => x.Id == bookingId);

        if (booking is null)
        {
            return NotFound("Prijava nije pronađena.");
        }

        if (booking.Status != FitnessSessionBookingStatus.PendingApproval)
        {
            return BadRequest("Odbijanje je moguće samo za prijavu koja čeka odobrenje.");
        }

        return await UpdateBookingStatus(
            bookingId,
            new UpdateFitnessSessionBookingStatusRequest
            {
                Status = (int)FitnessSessionBookingStatus.Rejected
            });
    }

    [HttpPost("bookings/{bookingId:long}/cancel-by-business")]
    public async Task<ActionResult<FitnessSessionBookingDto>> CancelBookingByBusiness(long bookingId)
    {
        var booking = await _db.FitnessSessionBookings
            .FirstOrDefaultAsync(x => x.Id == bookingId);

        if (booking is null)
        {
            return NotFound("Prijava nije pronađena.");
        }

        if (booking.Status != FitnessSessionBookingStatus.Booked &&
            booking.Status != FitnessSessionBookingStatus.PendingApproval)
        {
            return BadRequest("Otkazivanje je moguće samo za aktivnu prijavu.");
        }

        return await UpdateBookingStatus(
            bookingId,
            new UpdateFitnessSessionBookingStatusRequest
            {
                Status = (int)FitnessSessionBookingStatus.CancelledByBusiness
            });
    }

    [HttpPost("bookings/{bookingId:long}/attended")]
    public async Task<ActionResult<FitnessSessionBookingDto>> MarkBookingAttended(long bookingId)
    {
        var booking = await _db.FitnessSessionBookings
            .FirstOrDefaultAsync(x => x.Id == bookingId);

        if (booking is null)
        {
            return NotFound("Prijava nije pronađena.");
        }

        if (booking.Status != FitnessSessionBookingStatus.Booked)
        {
            return BadRequest("Dolazak se može označiti samo za prijavljenog člana.");
        }

        return await UpdateBookingStatus(
            bookingId,
            new UpdateFitnessSessionBookingStatusRequest
            {
                Status = (int)FitnessSessionBookingStatus.Attended
            });
    }

    [HttpPost("bookings/{bookingId:long}/no-show")]
    public async Task<ActionResult<FitnessSessionBookingDto>> MarkBookingNoShow(long bookingId)
    {
        var booking = await _db.FitnessSessionBookings
            .FirstOrDefaultAsync(x => x.Id == bookingId);

        if (booking is null)
        {
            return NotFound("Prijava nije pronađena.");
        }

        if (booking.Status != FitnessSessionBookingStatus.Booked)
        {
            return BadRequest("Nedolazak se može označiti samo za prijavljenog člana.");
        }

        return await UpdateBookingStatus(
            bookingId,
            new UpdateFitnessSessionBookingStatusRequest
            {
                Status = (int)FitnessSessionBookingStatus.NoShow
            });
    }

    // ============================================================
    // MEMBERS / ČLANOVI
    // ============================================================

    [HttpGet("businesses/{businessId:long}/members")]
    public async Task<ActionResult<List<FitnessMemberDto>>> GetMembers(
        long businessId,
        [FromQuery] string? search)
    {
        var query = _db.FitnessMembers
            .Include(x => x.Payments)
            .Where(x => x.BusinessId == businessId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();

            query = query.Where(x =>
                x.FullName.ToLower().Contains(term) ||
                (x.Phone != null && x.Phone.ToLower().Contains(term)) ||
                (x.Email != null && x.Email.ToLower().Contains(term)) ||
                (x.MemberCode != null && x.MemberCode.ToLower().Contains(term)));
        }

        var members = await query
            .OrderBy(x => x.FullName)
            .ToListAsync();

        return Ok(members.Select(ToMemberDto).ToList());
    }

    [HttpPost("businesses/{businessId:long}/members")]
    public async Task<ActionResult<FitnessMemberDto>> CreateMember(
        long businessId,
        CreateFitnessMemberRequest request)
    {
        if (!await BusinessExists(businessId))
        {
            return NotFound("Biznis nije pronađen.");
        }

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            return BadRequest("Ime i prezime člana je obavezno.");
        }

        if (!string.IsNullOrWhiteSpace(request.MemberCode))
        {
            var code = request.MemberCode.Trim();

            var exists = await _db.FitnessMembers
                .AnyAsync(x => x.BusinessId == businessId && x.MemberCode == code);

            if (exists)
            {
                return BadRequest("Član sa tom šifrom već postoji.");
            }
        }

        var now = DateTime.UtcNow;

        var member = new FitnessMember
        {
            BusinessId = businessId,
            CustomerProfileId = request.CustomerProfileId,
            BusinessCustomerId = request.BusinessCustomerId,
            AppUserId = request.AppUserId,
            FullName = request.FullName.Trim(),
            Phone = NormalizeNullableText(request.Phone),
            Email = NormalizeNullableText(request.Email),
            MemberCode = NormalizeNullableText(request.MemberCode),
            IsActive = request.IsActive,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _db.FitnessMembers.Add(member);
        await _db.SaveChangesAsync();

        member = await _db.FitnessMembers
            .Include(x => x.Payments)
            .FirstAsync(x => x.Id == member.Id);

        return Ok(ToMemberDto(member));
    }

    [HttpPut("businesses/{businessId:long}/members/{memberId:long}")]
    public async Task<ActionResult<FitnessMemberDto>> UpdateMember(
        long businessId,
        long memberId,
        UpdateFitnessMemberRequest request)
    {
        var member = await _db.FitnessMembers
            .Include(x => x.Payments)
            .FirstOrDefaultAsync(x => x.Id == memberId && x.BusinessId == businessId);

        if (member is null)
        {
            return NotFound("Član nije pronađen.");
        }

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            return BadRequest("Ime i prezime člana je obavezno.");
        }

        if (!string.IsNullOrWhiteSpace(request.MemberCode))
        {
            var code = request.MemberCode.Trim();

            var exists = await _db.FitnessMembers
                .AnyAsync(x =>
                    x.BusinessId == businessId &&
                    x.Id != memberId &&
                    x.MemberCode == code);

            if (exists)
            {
                return BadRequest("Član sa tom šifrom već postoji.");
            }
        }

        member.CustomerProfileId = request.CustomerProfileId;
        member.BusinessCustomerId = request.BusinessCustomerId;
        member.AppUserId = request.AppUserId;
        member.FullName = request.FullName.Trim();
        member.Phone = NormalizeNullableText(request.Phone);
        member.Email = NormalizeNullableText(request.Email);
        member.MemberCode = NormalizeNullableText(request.MemberCode);
        member.IsActive = request.IsActive;
        member.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(ToMemberDto(member));
    }

    // ============================================================
    // MEMBERSHIP PAYMENTS / UPLATE ČLANARINE
    // ============================================================

    [HttpGet("members/{memberId:long}/membership-payments")]
    public async Task<ActionResult<List<FitnessMembershipPaymentDto>>> GetMembershipPayments(long memberId)
    {
        var memberExists = await _db.FitnessMembers.AnyAsync(x => x.Id == memberId);

        if (!memberExists)
        {
            return NotFound("Član nije pronađen.");
        }

        var payments = await _db.FitnessMembershipPayments
            .Include(x => x.FitnessMember)
            .Where(x => x.FitnessMemberId == memberId)
            .OrderByDescending(x => x.PeriodEndDate)
            .ThenByDescending(x => x.PaidAtUtc)
            .ToListAsync();

        return Ok(payments.Select(ToPaymentDto).ToList());
    }

    [HttpPost("businesses/{businessId:long}/membership-payments")]
    public async Task<ActionResult<FitnessMembershipPaymentDto>> CreateMembershipPayment(
        long businessId,
        CreateFitnessMembershipPaymentRequest request)
    {
        var member = await _db.FitnessMembers
            .FirstOrDefaultAsync(x =>
                x.Id == request.FitnessMemberId &&
                x.BusinessId == businessId);

        if (member is null)
        {
            return NotFound("Član nije pronađen.");
        }

        if (request.Amount <= 0)
        {
            return BadRequest("Iznos članarine mora biti veći od 0.");
        }

        if (string.IsNullOrWhiteSpace(request.Currency))
        {
            return BadRequest("Valuta je obavezna.");
        }

        if (request.PeriodEndDate < request.PeriodStartDate)
        {
            return BadRequest("Datum završetka članarine ne može biti pre datuma početka.");
        }

        var now = DateTime.UtcNow;

        var payment = new FitnessMembershipPayment
        {
            BusinessId = businessId,
            FitnessMemberId = request.FitnessMemberId,
            Amount = request.Amount,
            Currency = request.Currency.Trim().ToUpperInvariant(),
            PeriodStartDate = request.PeriodStartDate,
            PeriodEndDate = request.PeriodEndDate,
            PaidAtUtc = request.PaidAtUtc ?? now,
            PaymentMethod = NormalizeNullableText(request.PaymentMethod),
            Note = NormalizeNullableText(request.Note),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _db.FitnessMembershipPayments.Add(payment);
        await _db.SaveChangesAsync();

        payment = await _db.FitnessMembershipPayments
            .Include(x => x.FitnessMember)
            .FirstAsync(x => x.Id == payment.Id);

        return Ok(ToPaymentDto(payment));
    }

    // ============================================================
    // MEMBERSHIP PLANS / PAKETI TRENINGA
    // ============================================================

    [HttpGet("businesses/{businessId:long}/membership-plans")]
    public async Task<ActionResult<List<FitnessMembershipPlanDto>>> GetMembershipPlans(
        long businessId)
    {
        var plans = await _db.FitnessMembershipPlans
            .AsNoTracking()
            .Include(x => x.FitnessClassType)
            .Where(x => x.BusinessId == businessId)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Name)
            .ToListAsync();

        return Ok(plans.Select(ToMembershipPlanDto).ToList());
    }

    [HttpPost("businesses/{businessId:long}/membership-plans")]
    public async Task<ActionResult<FitnessMembershipPlanDto>> CreateMembershipPlan(
        long businessId,
        CreateFitnessMembershipPlanRequest request)
    {
        if (!await BusinessExists(businessId))
        {
            return NotFound("Biznis nije pronađen.");
        }

        var validationError = await ValidateMembershipPlanRequest(
            businessId,
            request.FitnessClassTypeId,
            request.Name,
            request.TotalSessions,
            request.WeeklySessionLimit,
            request.DefaultValidityDays,
            request.Price,
            request.Currency);

        if (validationError is not null)
        {
            return BadRequest(validationError);
        }

        var name = request.Name.Trim();

        var exists = await _db.FitnessMembershipPlans
            .AnyAsync(x =>
                x.BusinessId == businessId &&
                x.Name.ToLower() == name.ToLower());

        if (exists)
        {
            return BadRequest("Paket treninga sa tim nazivom već postoji.");
        }

        var plan = new FitnessMembershipPlan
        {
            BusinessId = businessId,
            FitnessClassTypeId = request.FitnessClassTypeId,
            Name = name,
            TotalSessions = request.TotalSessions,
            WeeklySessionLimit = request.WeeklySessionLimit,
            DefaultValidityDays = request.DefaultValidityDays,
            Price = request.Price,
            Currency = request.Currency.Trim().ToUpperInvariant(),
            UnusedSessionsCarryOver = request.UnusedSessionsCarryOver,
            IsActive = request.IsActive,
            DisplayOrder = request.DisplayOrder,
            Note = NormalizeNullableText(request.Note)
        };

        _db.FitnessMembershipPlans.Add(plan);
        await _db.SaveChangesAsync();

        plan = await _db.FitnessMembershipPlans
            .AsNoTracking()
            .Include(x => x.FitnessClassType)
            .FirstAsync(x => x.Id == plan.Id);

        return Ok(ToMembershipPlanDto(plan));
    }

    [HttpPut("businesses/{businessId:long}/membership-plans/{planId:long}")]
    public async Task<ActionResult<FitnessMembershipPlanDto>> UpdateMembershipPlan(
        long businessId,
        long planId,
        UpdateFitnessMembershipPlanRequest request)
    {
        var plan = await _db.FitnessMembershipPlans
            .FirstOrDefaultAsync(x =>
                x.Id == planId &&
                x.BusinessId == businessId);

        if (plan is null)
        {
            return NotFound("Paket treninga nije pronađen.");
        }

        var validationError = await ValidateMembershipPlanRequest(
            businessId,
            request.FitnessClassTypeId,
            request.Name,
            request.TotalSessions,
            request.WeeklySessionLimit,
            request.DefaultValidityDays,
            request.Price,
            request.Currency);

        if (validationError is not null)
        {
            return BadRequest(validationError);
        }

        var name = request.Name.Trim();

        var exists = await _db.FitnessMembershipPlans
            .AnyAsync(x =>
                x.BusinessId == businessId &&
                x.Id != planId &&
                x.Name.ToLower() == name.ToLower());

        if (exists)
        {
            return BadRequest("Paket treninga sa tim nazivom već postoji.");
        }

        plan.FitnessClassTypeId = request.FitnessClassTypeId;
        plan.Name = name;
        plan.TotalSessions = request.TotalSessions;
        plan.WeeklySessionLimit = request.WeeklySessionLimit;
        plan.DefaultValidityDays = request.DefaultValidityDays;
        plan.Price = request.Price;
        plan.Currency = request.Currency.Trim().ToUpperInvariant();
        plan.UnusedSessionsCarryOver = request.UnusedSessionsCarryOver;
        plan.IsActive = request.IsActive;
        plan.DisplayOrder = request.DisplayOrder;
        plan.Note = NormalizeNullableText(request.Note);

        await _db.SaveChangesAsync();

        var updated = await _db.FitnessMembershipPlans
            .AsNoTracking()
            .Include(x => x.FitnessClassType)
            .FirstAsync(x => x.Id == plan.Id);

        return Ok(ToMembershipPlanDto(updated));
    }

    // ============================================================
    // MEMBER TRAINING PASSES / KUPLJENI PAKETI ČLANA
    // ============================================================

    [HttpGet("businesses/{businessId:long}/member-training-passes")]
    public async Task<ActionResult<List<FitnessMemberTrainingPassDto>>> GetMemberTrainingPasses(
        long businessId,
        [FromQuery] long? memberId,
        [FromQuery] bool activeOnly = false)
    {
        var query = _db.FitnessMemberTrainingPasses
            .AsNoTracking()
            .Include(x => x.FitnessMember)
            .Include(x => x.FitnessClassType)
            .Include(x => x.FitnessMembershipPlan)
            .Include(x => x.Bookings)
            .Include(x => x.SessionDebts)
            .Where(x => x.BusinessId == businessId);

        if (memberId.HasValue)
        {
            query = query.Where(x => x.FitnessMemberId == memberId.Value);
        }

        if (activeOnly)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            query = query.Where(x =>
                x.IsActive &&
                x.ValidFromDate <= today &&
                x.ValidToDate >= today);
        }

        var passes = await query
            .OrderByDescending(x => x.ValidToDate)
            .ThenByDescending(x => x.PaidAtUtc)
            .ToListAsync();

        return Ok(passes.Select(ToMemberTrainingPassDto).ToList());
    }

    [HttpGet("members/{memberId:long}/training-passes")]
    public async Task<ActionResult<List<FitnessMemberTrainingPassDto>>> GetMemberTrainingPassesForMember(
        long memberId)
    {
        var memberExists = await _db.FitnessMembers
            .AnyAsync(x => x.Id == memberId);

        if (!memberExists)
        {
            return NotFound("Član nije pronađen.");
        }

        var passes = await _db.FitnessMemberTrainingPasses
            .AsNoTracking()
            .Include(x => x.FitnessMember)
            .Include(x => x.FitnessClassType)
            .Include(x => x.FitnessMembershipPlan)
            .Include(x => x.Bookings)
            .Include(x => x.SessionDebts)
            .Where(x => x.FitnessMemberId == memberId)
            .OrderByDescending(x => x.PaidAtUtc)
            .ToListAsync();

        return Ok(passes.Select(ToMemberTrainingPassDto).ToList());
    }

    [HttpPost("businesses/{businessId:long}/member-training-passes")]
    public async Task<ActionResult<FitnessMemberTrainingPassDto>> CreateMemberTrainingPass(
        long businessId,
        CreateFitnessMemberTrainingPassRequest request)
    {
        var member = await _db.FitnessMembers
            .FirstOrDefaultAsync(x =>
                x.Id == request.FitnessMemberId &&
                x.BusinessId == businessId);

        if (member is null)
        {
            return NotFound("Član nije pronađen.");
        }

        if (!member.IsActive)
        {
            return BadRequest("Član nije aktivan.");
        }

        var plan = await _db.FitnessMembershipPlans
            .Include(x => x.FitnessClassType)
            .FirstOrDefaultAsync(x =>
                x.Id == request.FitnessMembershipPlanId &&
                x.BusinessId == businessId);

        if (plan is null)
        {
            return NotFound("Paket treninga nije pronađen.");
        }

        if (!plan.IsActive)
        {
            return BadRequest("Paket treninga nije aktivan.");
        }

        var validToDate = request.ValidToDate
            ?? request.ValidFromDate.AddDays(plan.DefaultValidityDays - 1);

        if (validToDate < request.ValidFromDate)
        {
            return BadRequest("Datum važenja do ne može biti pre datuma važenja od.");
        }

        var pricePaid = request.PricePaid ?? plan.Price;

        if (pricePaid < 0)
        {
            return BadRequest("Plaćeni iznos ne može biti negativan.");
        }

        var currency = string.IsNullOrWhiteSpace(request.Currency)
            ? plan.Currency
            : request.Currency.Trim().ToUpperInvariant();

        var now = DateTime.UtcNow;

        var pass = new FitnessMemberTrainingPass
        {
            BusinessId = businessId,
            FitnessMemberId = member.Id,
            FitnessMembershipPlanId = plan.Id,
            FitnessClassTypeId = plan.FitnessClassTypeId,
            PlanNameSnapshot = plan.Name,
            FitnessClassTypeNameSnapshot = plan.FitnessClassType?.Name,
            ValidFromDate = request.ValidFromDate,
            ValidToDate = validToDate,
            TotalSessions = plan.TotalSessions,
            WeeklySessionLimit = plan.WeeklySessionLimit,
            PricePaid = pricePaid,
            Currency = currency,
            PaidAtUtc = request.PaidAtUtc ?? now,
            IsActive = request.IsActive,
            Note = NormalizeNullableText(request.Note)
        };

        _db.FitnessMemberTrainingPasses.Add(pass);

        await _db.SaveChangesAsync();

        await SettleOpenSessionDebtsForTrainingPassAsync(pass.Id);

        pass = await _db.FitnessMemberTrainingPasses
            .AsNoTracking()
            .Include(x => x.FitnessMember)
            .Include(x => x.FitnessClassType)
            .Include(x => x.FitnessMembershipPlan)
            .Include(x => x.Bookings)
            .Include(x => x.SessionDebts)
            .FirstAsync(x => x.Id == pass.Id);

        return Ok(ToMemberTrainingPassDto(pass));
    }

    [HttpPut("businesses/{businessId:long}/member-training-passes/{passId:long}")]
    public async Task<ActionResult<FitnessMemberTrainingPassDto>> UpdateMemberTrainingPass(
        long businessId,
        long passId,
        UpdateFitnessMemberTrainingPassRequest request)
    {
        var pass = await _db.FitnessMemberTrainingPasses
            .FirstOrDefaultAsync(x =>
                x.Id == passId &&
                x.BusinessId == businessId);

        if (pass is null)
        {
            return NotFound("Kupljeni paket člana nije pronađen.");
        }

        if (request.ValidToDate < request.ValidFromDate)
        {
            return BadRequest("Datum važenja do ne može biti pre datuma važenja od.");
        }

        if (request.PricePaid < 0)
        {
            return BadRequest("Plaćeni iznos ne može biti negativan.");
        }

        if (string.IsNullOrWhiteSpace(request.Currency))
        {
            return BadRequest("Valuta je obavezna.");
        }

        pass.ValidFromDate = request.ValidFromDate;
        pass.ValidToDate = request.ValidToDate;
        pass.PricePaid = request.PricePaid;
        pass.Currency = request.Currency.Trim().ToUpperInvariant();
        pass.PaidAtUtc = request.PaidAtUtc;
        pass.IsActive = request.IsActive;
        pass.Note = NormalizeNullableText(request.Note);

        await _db.SaveChangesAsync();

        var updated = await _db.FitnessMemberTrainingPasses
            .AsNoTracking()
            .Include(x => x.FitnessMember)
            .Include(x => x.FitnessClassType)
            .Include(x => x.FitnessMembershipPlan)
            .Include(x => x.Bookings)
            .Include(x => x.SessionDebts)
            .FirstAsync(x => x.Id == pass.Id);

        return Ok(ToMemberTrainingPassDto(updated));
    }

    [HttpPost("businesses/{businessId:long}/member-training-passes/{passId:long}/void")]
    public async Task<ActionResult<FitnessMemberTrainingPassDto>> VoidMemberTrainingPass(
    long businessId,
    long passId,
    VoidFitnessMemberTrainingPassRequest request)
    {
        var pass = await _db.FitnessMemberTrainingPasses
            .Include(x => x.FitnessMember)
            .Include(x => x.Bookings)
            .Include(x => x.SessionDebts)
            .FirstOrDefaultAsync(x =>
                x.Id == passId &&
                x.BusinessId == businessId);

        if (pass is null)
        {
            return NotFound("Kupljeni paket člana nije pronađen.");
        }

        if (pass.IsVoided)
        {
            return BadRequest("Kupljeni paket je već storniran.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return BadRequest("Unesite razlog storniranja.");
        }

        var hasUsedBookings = pass.Bookings.Any(x =>
            x.ConsumesTrainingPassSession &&
            x.Status != FitnessSessionBookingStatus.CancelledByBusiness &&
            x.Status != FitnessSessionBookingStatus.CancelledByCustomer &&
            x.Status != FitnessSessionBookingStatus.Rejected);

        if (hasUsedBookings)
        {
            return BadRequest("Nije moguće stornirati paket koji već ima aktivne iskorišćene termine. Prvo uklonite prijave ako je potrebno.");
        }

        var hasSettledDebts = pass.SessionDebts.Any(x =>
    x.Status == FitnessMemberSessionDebtStatus.Settled);

        if (hasSettledDebts)
        {
            return BadRequest("Nije moguće stornirati paket koji već ima prebijene zadužene termine.");
        }

        pass.IsVoided = true;
        pass.IsActive = false;
        pass.VoidedAtUtc = DateTime.UtcNow;
        pass.VoidReason = request.Reason.Trim();
        pass.VoidedByUserId = request.VoidedByUserId;

        await _db.SaveChangesAsync();

        return Ok(ToMemberTrainingPassDto(pass));
    }

    // ============================================================
    // REPORTS / IZVEŠTAJI
    // ============================================================

    [HttpGet("businesses/{businessId:long}/daily-report")]
    public async Task<ActionResult<FitnessDailyReportDto>> GetDailyReport(
        long businessId,
        [FromQuery] DateOnly? date,
        CancellationToken cancellationToken)
    {
        if (!await BusinessExists(businessId))
        {
            return NotFound("Biznis nije pronađen.");
        }

        var reportDate = date ?? DateOnly.FromDateTime(DateTime.Now);

        var fromLocal = reportDate.ToDateTime(TimeOnly.MinValue);
        var toLocalExclusive = reportDate.AddDays(1).ToDateTime(TimeOnly.MinValue);

        var fromUtc = DateTime.SpecifyKind(fromLocal, DateTimeKind.Local).ToUniversalTime();
        var toUtc = DateTime.SpecifyKind(toLocalExclusive, DateTimeKind.Local).ToUniversalTime();

        var today = DateOnly.FromDateTime(DateTime.Now);
        var expiringUntil = today.AddDays(7);
        var nowUtc = DateTime.UtcNow;

        var sessions = await _db.FitnessSessions
            .AsNoTracking()
            .Include(x => x.FitnessRoom)
            .Include(x => x.FitnessClassType)
            .Include(x => x.Bookings)
            .Where(x =>
                x.BusinessId == businessId &&
                x.StartAtUtc >= fromUtc &&
                x.StartAtUtc < toUtc)
            .OrderBy(x => x.StartAtUtc)
            .ThenBy(x => x.FitnessRoom.Name)
            .ToListAsync(cancellationToken);

        var activeMembersCount = await _db.FitnessMembers
            .AsNoTracking()
            .CountAsync(x =>
                x.BusinessId == businessId &&
                x.IsActive,
                cancellationToken);

        var activeTrainingPassesCount = await _db.FitnessMemberTrainingPasses
            .AsNoTracking()
            .CountAsync(x =>
                x.BusinessId == businessId &&
                x.IsActive &&
                !x.IsVoided &&
                x.ValidFromDate <= today &&
                x.ValidToDate >= today,
                cancellationToken);

        var trainingPassesExpiringSoonCount = await _db.FitnessMemberTrainingPasses
            .AsNoTracking()
            .CountAsync(x =>
                x.BusinessId == businessId &&
                x.IsActive &&
                !x.IsVoided &&
                x.ValidToDate >= today &&
                x.ValidToDate <= expiringUntil,
                cancellationToken);

        var expiredTrainingPassesCount = await _db.FitnessMemberTrainingPasses
            .AsNoTracking()
            .CountAsync(x =>
                x.BusinessId == businessId &&
                x.IsActive &&
                !x.IsVoided &&
                x.ValidToDate < today,
                cancellationToken);

        var openSessionDebtsCount = await _db.FitnessMemberSessionDebts
            .AsNoTracking()
            .CountAsync(x =>
                x.BusinessId == businessId &&
                x.Status == FitnessMemberSessionDebtStatus.Open,
                cancellationToken);

        var unreadChatCount = await _db.ChatConversations
            .AsNoTracking()
            .Where(x =>
                x.BusinessId == businessId &&
                x.IsActive)
            .SumAsync(x => (int?)x.UnreadForBusinessCount, cancellationToken) ?? 0;

        var openNotificationsCount = await _db.BusinessActivityNotifications
            .AsNoTracking()
            .CountAsync(x =>
                x.BusinessId == businessId &&
                x.RecipientKey == "business" &&
                x.Domain == BusinessActivityNotificationDomains.Fitness &&
                !x.IsResolved,
                cancellationToken);

        var reportSessions = sessions
            .Where(x => x.StartAtUtc >= nowUtc || reportDate != today)
            .OrderBy(x => x.StartAtUtc)
            .Take(10)
            .Select(x =>
            {
                var bookedCount = CountReportActiveBookings(x.Bookings);
                var localStart = x.StartAtUtc.ToLocalTime();
                var localEnd = x.EndAtUtc.ToLocalTime();

                var roomName = string.IsNullOrWhiteSpace(x.FitnessRoom?.Name)
                    ? $"Sala #{x.FitnessRoomId}"
                    : x.FitnessRoom.Name;

                var className = string.IsNullOrWhiteSpace(x.FitnessClassType?.Name)
                    ? "Trening"
                    : x.FitnessClassType.Name;

                return new FitnessDailyReportSessionDto
                {
                    FitnessSessionId = x.Id,
                    TimeText = $"{localStart:HH:mm} - {localEnd:HH:mm}",
                    RoomName = roomName,
                    ClassName = className,
                    SessionTypeText = GetSessionTypeText(x.SessionType),
                    Capacity = x.Capacity,
                    BookedCount = bookedCount,
                    CapacityText = x.SessionType == FitnessSessionType.Individual
                        ? bookedCount > 0 ? "zauzeto" : "slobodno"
                        : $"{bookedCount}/{x.Capacity}",
                    IsFull = bookedCount >= x.Capacity
                };
            })
            .ToList();

        var bookingsCount = sessions.Sum(x => CountReportActiveBookings(x.Bookings));

        var pendingApprovalBookingsCount = sessions.Sum(x =>
            x.Bookings.Count(b => b.Status == FitnessSessionBookingStatus.PendingApproval));

        var fullSessionsCount = sessions.Count(x =>
            CountReportActiveBookings(x.Bookings) >= x.Capacity);

        var freeSpotsCount = sessions.Sum(x =>
            Math.Max(0, x.Capacity - CountReportActiveBookings(x.Bookings)));

        return Ok(new FitnessDailyReportDto
        {
            BusinessId = businessId,
            Date = reportDate,
            SessionsCount = sessions.Count,
            BookingsCount = bookingsCount,
            PendingApprovalBookingsCount = pendingApprovalBookingsCount,
            FullSessionsCount = fullSessionsCount,
            FreeSpotsCount = freeSpotsCount,
            ActiveMembersCount = activeMembersCount,
            ActiveTrainingPassesCount = activeTrainingPassesCount,
            TrainingPassesExpiringSoonCount = trainingPassesExpiringSoonCount,
            ExpiredTrainingPassesCount = expiredTrainingPassesCount,
            OpenSessionDebtsCount = openSessionDebtsCount,
            UnreadChatCount = unreadChatCount,
            OpenNotificationsCount = openNotificationsCount,
            UpcomingSessions = reportSessions
        });
    }

    [HttpGet("businesses/{businessId:long}/inactive-members")]
    public async Task<ActionResult<List<FitnessInactiveMemberDto>>> GetInactiveMembers(
        long businessId,
        [FromQuery] int inactiveDays = 14,
        CancellationToken cancellationToken = default)
    {
        if (inactiveDays < 1)
        {
            inactiveDays = 1;
        }

        if (inactiveDays > 365)
        {
            inactiveDays = 365;
        }

        if (!await BusinessExists(businessId))
        {
            return NotFound("Biznis nije pronađen.");
        }

        var nowUtc = DateTime.UtcNow;
        var cutoffUtc = nowUtc.AddDays(-inactiveDays);
        var today = DateOnly.FromDateTime(DateTime.Now);

        var members = await _db.FitnessMembers
            .AsNoTracking()
            .Where(x =>
                x.BusinessId == businessId &&
                x.IsActive)
            .OrderBy(x => x.FullName)
            .ToListAsync(cancellationToken);

        var memberIds = members
            .Select(x => x.Id)
            .ToList();

        var lastBookings = await _db.FitnessSessionBookings
            .AsNoTracking()
            .Where(x =>
                x.BusinessId == businessId &&
                x.FitnessMemberId.HasValue &&
                memberIds.Contains(x.FitnessMemberId.Value) &&
                x.FitnessSession.StartAtUtc <= nowUtc &&
                (
                    x.Status == FitnessSessionBookingStatus.Booked ||
                    x.Status == FitnessSessionBookingStatus.Attended ||
                    x.Status == FitnessSessionBookingStatus.NoShow
                ))
            .GroupBy(x => x.FitnessMemberId!.Value)
            .Select(g => new
            {
                FitnessMemberId = g.Key,
                LastBookingStartAtUtc = g.Max(x => x.FitnessSession.StartAtUtc)
            })
            .ToDictionaryAsync(
                x => x.FitnessMemberId,
                x => x.LastBookingStartAtUtc,
                cancellationToken);

        var activePasses = await _db.FitnessMemberTrainingPasses
            .AsNoTracking()
            .Where(x =>
                x.BusinessId == businessId &&
                memberIds.Contains(x.FitnessMemberId) &&
                x.IsActive &&
                !x.IsVoided &&
                x.ValidFromDate <= today &&
                x.ValidToDate >= today)
            .GroupBy(x => x.FitnessMemberId)
            .Select(g => new
            {
                FitnessMemberId = g.Key,
                ValidUntil = g.Max(x => x.ValidToDate)
            })
            .ToDictionaryAsync(
                x => x.FitnessMemberId,
                x => x.ValidUntil,
                cancellationToken);

        var openDebts = await _db.FitnessMemberSessionDebts
            .AsNoTracking()
            .Where(x =>
                x.BusinessId == businessId &&
                memberIds.Contains(x.FitnessMemberId) &&
                x.Status == FitnessMemberSessionDebtStatus.Open)
            .Select(x => x.FitnessMemberId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var openDebtSet = openDebts.ToHashSet();

        var result = new List<FitnessInactiveMemberDto>();

        foreach (var member in members)
        {
            lastBookings.TryGetValue(member.Id, out var lastBookingStartAtUtc);

            var isInactive =
                lastBookingStartAtUtc == default ||
                lastBookingStartAtUtc < cutoffUtc;

            if (!isInactive)
            {
                continue;
            }

            var inactiveDaysCount = lastBookingStartAtUtc == default
                ? (int?)null
                : Math.Max(0, (int)Math.Floor((nowUtc - lastBookingStartAtUtc).TotalDays));

            activePasses.TryGetValue(member.Id, out var validUntil);

            var hasActivePass = validUntil != default;
            var hasDebt = openDebtSet.Contains(member.Id);

            var statusParts = new List<string>();

            if (inactiveDaysCount.HasValue)
            {
                statusParts.Add($"Neaktivan {inactiveDaysCount.Value} dana");
            }
            else
            {
                statusParts.Add("Nema evidentiran dolazak");
            }

            statusParts.Add(hasActivePass
                ? $"Paket aktivan do {validUntil:dd.MM.yyyy}"
                : "Nema aktivan paket");

            if (hasDebt)
            {
                statusParts.Add("Ima dugovanje");
            }

            result.Add(new FitnessInactiveMemberDto
            {
                MemberId = member.Id,
                FullName = member.FullName,
                Phone = member.Phone,
                Email = member.Email,
                LastBookingStartAtUtc = lastBookingStartAtUtc == default
                    ? null
                    : lastBookingStartAtUtc,
                InactiveDays = inactiveDaysCount,
                HasActiveTrainingPass = hasActivePass,
                TrainingPassValidUntil = hasActivePass ? validUntil : null,
                HasOpenDebt = hasDebt,
                BusinessCustomerId = member.BusinessCustomerId,
                CustomerProfileId = member.CustomerProfileId,
                AppUserId = member.AppUserId,
                StatusText = string.Join(" | ", statusParts)
            });
        }

        return Ok(result
            .OrderByDescending(x => x.InactiveDays ?? int.MaxValue)
            .ThenBy(x => x.FullName)
            .ToList());
    }

    // ============================================================
    // HELPERS
    // ============================================================


    private async Task UpsertFitnessNotificationsForBookingAsync(
    FitnessSession session,
    FitnessSessionBooking booking,
    FitnessMemberSessionDebt? createdOpenDebt,
    string? warningText)
    {
        var nowUtc = DateTime.UtcNow;
        var mainText = BuildFitnessNotificationSessionText(session);
        var customerText = BuildFitnessNotificationCustomerText(booking.CustomerName, booking.CustomerPhone);

        if (booking.Status == FitnessSessionBookingStatus.PendingApproval)
        {
            await UpsertBusinessNotificationAsync(new BusinessActivityNotification
            {
                BusinessId = booking.BusinessId,
                RecipientType = BusinessActivityNotificationRecipients.Business,
                RecipientKey = "business",
                Domain = BusinessActivityNotificationDomains.Fitness,
                Kind = BusinessActivityNotificationKinds.PendingApproval,
                ActivityKey = FitnessPendingBookingNotificationKey(booking.Id),
                Title = "Prijava čeka odobrenje",
                MainText = mainText,
                PreviewText = customerText,
                Priority = 120,
                SortAtUtc = booking.CreatedAtUtc,
                FitnessSessionId = booking.FitnessSessionId,
                FitnessSessionBookingId = booking.Id,
                FitnessMemberId = booking.FitnessMemberId,
                CustomerProfileId = booking.CustomerProfileId,
                BusinessCustomerId = booking.BusinessCustomerId,
                CustomerName = booking.CustomerName,
                CustomerPhone = booking.CustomerPhone,
                CreatedAtUtc = nowUtc,
                UpdatedAtUtc = nowUtc
            });
        }

        if (!booking.MembershipWasActiveAtBooking &&
            createdOpenDebt is null)
        {
            await UpsertBusinessNotificationAsync(new BusinessActivityNotification
            {
                BusinessId = booking.BusinessId,
                RecipientType = BusinessActivityNotificationRecipients.Business,
                RecipientKey = "business",
                Domain = BusinessActivityNotificationDomains.Fitness,
                Kind = BusinessActivityNotificationKinds.MembershipWarning,
                ActivityKey = FitnessMembershipWarningNotificationKey(booking.Id),
                Title = "Prijava bez aktivnog paketa",
                MainText = mainText,
                PreviewText = $"{customerText} | {NormalizeNotificationText(warningText, "Član nema aktivan paket.")}",
                Priority = 100,
                SortAtUtc = booking.CreatedAtUtc,
                FitnessSessionId = booking.FitnessSessionId,
                FitnessSessionBookingId = booking.Id,
                FitnessMemberId = booking.FitnessMemberId,
                CustomerProfileId = booking.CustomerProfileId,
                BusinessCustomerId = booking.BusinessCustomerId,
                CustomerName = booking.CustomerName,
                CustomerPhone = booking.CustomerPhone,
                CreatedAtUtc = nowUtc,
                UpdatedAtUtc = nowUtc
            });
        }

        if (createdOpenDebt is not null)
        {
            await UpsertBusinessNotificationAsync(new BusinessActivityNotification
            {
                BusinessId = booking.BusinessId,
                RecipientType = BusinessActivityNotificationRecipients.Business,
                RecipientKey = "business",
                Domain = BusinessActivityNotificationDomains.Fitness,
                Kind = BusinessActivityNotificationKinds.OpenDebt,
                ActivityKey = FitnessOpenDebtNotificationKey(createdOpenDebt.Id),
                Title = "Otvoren zaduženi termin",
                MainText = mainText,
                PreviewText = $"{customerText} | Član nema aktivan paket. Termin je evidentiran kao zaduženje.",
                Priority = 110,
                SortAtUtc = booking.CreatedAtUtc,
                FitnessSessionId = booking.FitnessSessionId,
                FitnessSessionBookingId = booking.Id,
                FitnessMemberId = booking.FitnessMemberId,
                FitnessMemberSessionDebtId = createdOpenDebt.Id,
                CustomerProfileId = booking.CustomerProfileId,
                BusinessCustomerId = booking.BusinessCustomerId,
                CustomerName = booking.CustomerName,
                CustomerPhone = booking.CustomerPhone,
                CreatedAtUtc = nowUtc,
                UpdatedAtUtc = nowUtc
            });
        }
    }

    private async Task UpsertBusinessNotificationAsync(BusinessActivityNotification source)
    {
        var existing = await _db.BusinessActivityNotifications
            .FirstOrDefaultAsync(x =>
                x.BusinessId == source.BusinessId &&
                x.RecipientKey == source.RecipientKey &&
                x.ActivityKey == source.ActivityKey);

        var nowUtc = DateTime.UtcNow;

        if (existing is null)
        {
            source.CreatedAtUtc = source.CreatedAtUtc == default ? nowUtc : source.CreatedAtUtc;
            source.UpdatedAtUtc = nowUtc;
            source.IsResolved = false;
            source.ResolvedAtUtc = null;
            source.ResolvedByUserId = null;

            _db.BusinessActivityNotifications.Add(source);
            return;
        }

        existing.RecipientType = source.RecipientType;
        existing.RecipientKey = source.RecipientKey;
        existing.RecipientAppUserId = source.RecipientAppUserId;
        existing.RecipientCustomerProfileId = source.RecipientCustomerProfileId;
        existing.RecipientStaffMemberId = source.RecipientStaffMemberId;
        existing.RecipientOperationUnitId = source.RecipientOperationUnitId;

        existing.Domain = source.Domain;
        existing.Kind = source.Kind;
        existing.Title = source.Title;
        existing.MainText = source.MainText;
        existing.PreviewText = source.PreviewText;
        existing.Priority = source.Priority;
        existing.SortAtUtc = source.SortAtUtc == default ? nowUtc : source.SortAtUtc;

        existing.AppointmentId = source.AppointmentId;
        existing.ChangeRequestId = source.ChangeRequestId;
        existing.RestaurantOrderId = source.RestaurantOrderId;
        existing.RestaurantTableReservationId = source.RestaurantTableReservationId;
        existing.RestaurantAreaReservationId = source.RestaurantAreaReservationId;
        existing.ConversationId = source.ConversationId;
        existing.ChatMessageId = source.ChatMessageId;
        existing.SystemAlarmTriggerId = source.SystemAlarmTriggerId;
        existing.FitnessSessionId = source.FitnessSessionId;
        existing.FitnessSessionBookingId = source.FitnessSessionBookingId;
        existing.FitnessMemberId = source.FitnessMemberId;
        existing.FitnessMemberSessionDebtId = source.FitnessMemberSessionDebtId;
        existing.CustomerProfileId = source.CustomerProfileId;
        existing.BusinessCustomerId = source.BusinessCustomerId;
        existing.CustomerName = source.CustomerName;
        existing.CustomerPhone = source.CustomerPhone;
        existing.PayloadJson = source.PayloadJson;

        existing.IsResolved = false;
        existing.ResolvedAtUtc = null;
        existing.ResolvedByUserId = null;
        existing.UpdatedAtUtc = nowUtc;
    }

    private async Task ResolveBusinessNotificationByKeyAsync(
        long businessId,
        string activityKey,
        DateTime nowUtc)
    {
        var notification = await _db.BusinessActivityNotifications
            .FirstOrDefaultAsync(x =>
                x.BusinessId == businessId &&
                x.RecipientKey == "business" &&
                x.ActivityKey == activityKey);

        if (notification is null)
            return;

        notification.IsResolved = true;
        notification.ResolvedAtUtc = nowUtc;
        notification.IsSeen = true;
        notification.SeenAtUtc ??= nowUtc;
        notification.SnoozedUntilUtc = null;
        notification.UpdatedAtUtc = nowUtc;
    }

    private static string FitnessPendingBookingNotificationKey(long bookingId)
    {
        return $"fitness.booking.pending:{bookingId}";
    }

    private static string FitnessMembershipWarningNotificationKey(long bookingId)
    {
        return $"fitness.booking.membership-warning:{bookingId}";
    }

    private static string FitnessOpenDebtNotificationKey(long debtId)
    {
        return $"fitness.session-debt.open:{debtId}";
    }

    private static string BuildFitnessNotificationSessionText(FitnessSession session)
    {
        var startLocal = session.StartAtUtc.ToLocalTime();
        var endLocal = session.EndAtUtc.ToLocalTime();

        var roomName = string.IsNullOrWhiteSpace(session.FitnessRoom?.Name)
            ? $"Sala #{session.FitnessRoomId}"
            : session.FitnessRoom.Name;

        var className = string.IsNullOrWhiteSpace(session.FitnessClassType?.Name)
            ? "Trening"
            : session.FitnessClassType.Name;

        return $"{startLocal:dd.MM.yyyy HH:mm} - {endLocal:HH:mm} | {roomName} | {className}";
    }

    private static string BuildFitnessNotificationCustomerText(string? customerName, string? customerPhone)
    {
        return $"Član: {NormalizeNotificationText(customerName, "Član")} | Telefon: {NormalizeNotificationText(customerPhone, "-")}";
    }

    private static string NormalizeNotificationText(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Trim();
    }
    private async Task<bool> BusinessExists(long businessId)
    {
        return await _db.Businesses.AnyAsync(x => x.Id == businessId);
    }

    private async Task SettleOpenSessionDebtsForTrainingPassAsync(long trainingPassId)
    {
        var pass = await _db.FitnessMemberTrainingPasses
            .Include(x => x.FitnessMember)
            .FirstOrDefaultAsync(x => x.Id == trainingPassId);

        if (pass is null || pass.IsVoided || !pass.IsActive)
        {
            return;
        }

        var openDebtsQuery = _db.FitnessMemberSessionDebts
            .Where(x =>
                x.BusinessId == pass.BusinessId &&
                x.FitnessMemberId == pass.FitnessMemberId &&
                x.Status == FitnessMemberSessionDebtStatus.Open &&
                x.FitnessMemberTrainingPassId == null);

        if (pass.FitnessClassTypeId.HasValue)
        {
            openDebtsQuery = openDebtsQuery.Where(x =>
                x.FitnessClassTypeId == null ||
                x.FitnessClassTypeId == pass.FitnessClassTypeId.Value);
        }

        var openDebts = await openDebtsQuery
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync();

        if (openDebts.Count == 0)
        {
            return;
        }

        var alreadyUsed = await _db.FitnessSessionBookings
            .CountAsync(x =>
                x.FitnessMemberTrainingPassId == pass.Id &&
                x.ConsumesTrainingPassSession &&
                x.Status != FitnessSessionBookingStatus.CancelledByBusiness &&
                x.Status != FitnessSessionBookingStatus.CancelledByCustomer &&
                x.Status != FitnessSessionBookingStatus.Rejected);

        var alreadySettledDebts = await _db.FitnessMemberSessionDebts
            .Where(x =>
                x.FitnessMemberTrainingPassId == pass.Id &&
                x.Status == FitnessMemberSessionDebtStatus.Settled)
            .SumAsync(x => (int?)x.SessionsCount) ?? 0;

        var usedTotal = alreadyUsed + alreadySettledDebts;

        var remaining = pass.TotalSessions.HasValue
            ? Math.Max(0, pass.TotalSessions.Value - usedTotal)
            : int.MaxValue;

        if (remaining <= 0)
        {
            return;
        }

        var now = DateTime.UtcNow;

        foreach (var debt in openDebts)
        {
            if (remaining <= 0)
                break;

            if (debt.SessionsCount > remaining)
                break;

            debt.Status = FitnessMemberSessionDebtStatus.Settled;
            debt.FitnessMemberTrainingPassId = pass.Id;
            debt.SettledAtUtc = now;
            debt.UpdatedAtUtc = now;

            await ResolveBusinessNotificationByKeyAsync(
                debt.BusinessId,
                FitnessOpenDebtNotificationKey(debt.Id),
                now);

            remaining -= debt.SessionsCount;
        }

        await _db.SaveChangesAsync();
    }

    private async Task<FitnessSettings> GetOrCreateSettings(long businessId)
    {
        var settings = await _db.FitnessSettings
            .FirstOrDefaultAsync(x => x.BusinessId == businessId);

        if (settings is not null)
        {
            return settings;
        }

        settings = CreateDefaultSettings(businessId);

        _db.FitnessSettings.Add(settings);
        await _db.SaveChangesAsync();

        return settings;
    }

    private static FitnessSettings CreateDefaultSettings(long businessId)
    {
        var now = DateTime.UtcNow;

        return new FitnessSettings
        {
            BusinessId = businessId,
GroupClassesEnabled = true,
IndividualTrainingEnabled = false,
ReceivesCustomerMessages = true,
MembershipsEnabled = true,
            UnpaidMembershipBookingPolicy = FitnessUnpaidMembershipBookingPolicy.AllowWithNotification,
            DefaultMembershipDurationDays = 30,
            AllowCustomerCancelBooking = true,
            CustomerCancelDeadlineMinutes = 120,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    private async Task<string?> ValidateSessionRequest(
        long businessId,
        long? sessionId,
        long fitnessRoomId,
        long? fitnessClassTypeId,
        long? trainerStaffMemberId,
        int sessionTypeValue,
        DateTime startAtUtc,
        DateTime endAtUtc,
        int capacity)
    {
        if (!Enum.IsDefined(typeof(FitnessSessionType), sessionTypeValue))
        {
            return "Nepoznat tip termina.";
        }

        if (startAtUtc >= endAtUtc)
        {
            return "Vreme završetka mora biti posle vremena početka.";
        }

        if (capacity <= 0)
        {
            return "Kapacitet mora biti veći od 0.";
        }

        var sessionType = (FitnessSessionType)sessionTypeValue;

        if (sessionType == FitnessSessionType.Individual && capacity != 1)
        {
            return "Individualni trening mora imati kapacitet 1.";
        }

        var room = await _db.FitnessRooms
            .FirstOrDefaultAsync(x =>
                x.Id == fitnessRoomId &&
                x.BusinessId == businessId);

        if (room is null)
        {
            return "Sala nije pronađena.";
        }

        if (!room.IsActive)
        {
            return "Sala nije aktivna.";
        }

        if (sessionType == FitnessSessionType.Group && !room.AllowsGroupClasses)
        {
            return "Ova sala ne dozvoljava grupne treninge.";
        }

        if (sessionType == FitnessSessionType.Individual && !room.AllowsIndividualTraining)
        {
            return "Ova sala ne dozvoljava individualne treninge.";
        }

        if (capacity > room.Capacity)
        {
            return $"Kapacitet termina ne može biti veći od kapaciteta sale ({room.Capacity}).";
        }

        if (fitnessClassTypeId is not null)
        {
            var classTypeExists = await _db.FitnessClassTypes
                .AnyAsync(x =>
                    x.Id == fitnessClassTypeId &&
                    x.BusinessId == businessId &&
                    x.IsActive);

            if (!classTypeExists)
            {
                return "Tip treninga nije pronađen ili nije aktivan.";
            }
        }

        if (trainerStaffMemberId is not null)
        {
            var trainerExists = await _db.StaffMembers
                .AnyAsync(x =>
                    x.Id == trainerStaffMemberId &&
                    x.BusinessId == businessId);

            if (!trainerExists)
            {
                return "Trener nije pronađen.";
            }
        }

        var overlaps = await _db.FitnessSessions
            .AnyAsync(x =>
                x.BusinessId == businessId &&
                x.FitnessRoomId == fitnessRoomId &&
                x.Status == FitnessSessionStatus.Scheduled &&
                (!sessionId.HasValue || x.Id != sessionId.Value) &&
                startAtUtc < x.EndAtUtc &&
                endAtUtc > x.StartAtUtc);

        if (overlaps)
        {
            return "U ovoj sali već postoji termin koji se preklapa sa izabranim vremenom.";
        }

        return null;
    }

    private async Task<FitnessSession?> LoadSession(long sessionId)
    {
        return await _db.FitnessSessions
            .Include(x => x.FitnessRoom)
            .Include(x => x.FitnessClassType)
            .Include(x => x.Bookings)
            .FirstOrDefaultAsync(x => x.Id == sessionId);
    }

    private static string? ValidateRoomRequest(string name, int capacity)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Naziv sale je obavezan.";
        }

        if (capacity <= 0)
        {
            return "Kapacitet sale mora biti veći od 0.";
        }

        return null;
    }

    private static string? ValidateClassTypeRequest(
        string name,
        int defaultDurationMin,
        int? defaultCapacity)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Naziv tipa treninga je obavezan.";
        }

        if (defaultDurationMin <= 0)
        {
            return "Podrazumevano trajanje treninga mora biti veće od 0 minuta.";
        }

        if (defaultCapacity is not null && defaultCapacity <= 0)
        {
            return "Podrazumevani kapacitet mora biti veći od 0.";
        }

        return null;
    }

    private async Task<string?> ValidateMembershipPlanRequest(
    long businessId,
    long? fitnessClassTypeId,
    string name,
    int? totalSessions,
    int? weeklySessionLimit,
    int defaultValidityDays,
    decimal price,
    string currency)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Naziv paketa treninga je obavezan.";
        }

        if (totalSessions.HasValue && totalSessions.Value <= 0)
        {
            return "Broj termina mora biti veći od 0. Za neograničeno ostavite prazno.";
        }

        if (weeklySessionLimit.HasValue && weeklySessionLimit.Value <= 0)
        {
            return "Nedeljni limit mora biti veći od 0. Ako nema limita, ostavite prazno.";
        }

        if (totalSessions.HasValue &&
            weeklySessionLimit.HasValue &&
            weeklySessionLimit.Value > totalSessions.Value)
        {
            return "Nedeljni limit ne može biti veći od ukupnog broja termina.";
        }

        if (defaultValidityDays <= 0)
        {
            return "Trajanje paketa mora biti veće od 0 dana.";
        }

        if (price < 0)
        {
            return "Cena paketa ne može biti negativna.";
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            return "Valuta je obavezna.";
        }

        if (fitnessClassTypeId.HasValue)
        {
            var classTypeExists = await _db.FitnessClassTypes
                .AnyAsync(x =>
                    x.Id == fitnessClassTypeId.Value &&
                    x.BusinessId == businessId);

            if (!classTypeExists)
            {
                return "Tip treninga nije pronađen.";
            }
        }

        return null;
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

    private async Task<TrainingPassBookingCheckResult> FindUsableTrainingPassForBooking(
    long businessId,
    long? fitnessMemberId,
    long? customerProfileId,
    long? businessCustomerId,
    long? appUserId,
    string? customerPhone,
    FitnessSession session,
    DateTime nowUtc)
    {
        var memberQuery = _db.FitnessMembers
            .Where(x =>
                x.BusinessId == businessId &&
                x.IsActive);

        if (fitnessMemberId is not null)
        {
            memberQuery = memberQuery.Where(x => x.Id == fitnessMemberId.Value);
        }
        else if (customerProfileId is not null)
        {
            memberQuery = memberQuery.Where(x => x.CustomerProfileId == customerProfileId.Value);
        }
        else if (businessCustomerId is not null)
        {
            memberQuery = memberQuery.Where(x => x.BusinessCustomerId == businessCustomerId.Value);
        }
        else if (appUserId is not null)
        {
            memberQuery = memberQuery.Where(x => x.AppUserId == appUserId.Value);
        }
        else if (!string.IsNullOrWhiteSpace(customerPhone))
        {
            var phone = customerPhone.Trim();
            memberQuery = memberQuery.Where(x => x.Phone == phone);
        }
        else
        {
            return new TrainingPassBookingCheckResult
            {
                CanUse = false,
                Message = "Član nije pronađen."
            };
        }

        var member = await memberQuery.FirstOrDefaultAsync();

        if (member is null)
        {
            return new TrainingPassBookingCheckResult
            {
                CanUse = false,
                Message = "Član nije pronađen ili nije aktivan."
            };
        }

        var sessionDate = DateOnly.FromDateTime(session.StartAtUtc.ToLocalTime());

        var passes = await _db.FitnessMemberTrainingPasses
            .Include(x => x.Bookings)
                .ThenInclude(x => x.FitnessSession)
            .Include(x => x.SessionDebts)
            .Where(x =>
                x.BusinessId == businessId &&
                x.FitnessMemberId == member.Id &&
                x.IsActive &&
                !x.IsVoided &&
                x.ValidFromDate <= sessionDate &&
                x.ValidToDate >= sessionDate)
            .OrderBy(x => x.ValidToDate)
            .ThenBy(x => x.Id)
            .ToListAsync();

        if (passes.Count == 0)
        {
            return new TrainingPassBookingCheckResult
            {
                CanUse = false,
                Message = "Član nema aktivan kupljeni paket za datum ovog termina."
            };
        }

        var matchingPasses = passes
            .Where(x =>
                x.FitnessClassTypeId == null ||
                session.FitnessClassTypeId == null ||
                x.FitnessClassTypeId == session.FitnessClassTypeId)
            .ToList();

        if (matchingPasses.Count == 0)
        {
            return new TrainingPassBookingCheckResult
            {
                CanUse = false,
                Message = "Član nema aktivan paket za ovaj tip treninga."
            };
        }

        var weekStart = GetWeekStart(sessionDate);
        var weekEnd = weekStart.AddDays(6);

        foreach (var pass in matchingPasses)
        {
            var usedTotal =
                CountUsedTrainingPassSessions(pass.Bookings) +
                CountSettledSessionDebts(pass.SessionDebts);

            if (pass.TotalSessions.HasValue &&
                usedTotal >= pass.TotalSessions.Value)
            {
                continue;
            }

            var usedThisWeek = CountUsedTrainingPassSessionsInSessionPeriod(
                pass.Bookings,
                weekStart,
                weekEnd);

            if (pass.WeeklySessionLimit.HasValue &&
                usedThisWeek >= pass.WeeklySessionLimit.Value)
            {
                continue;
            }

            return new TrainingPassBookingCheckResult
            {
                CanUse = true,
                Message = "Paket je validan.",
                TrainingPass = pass
            };
        }

        var firstPass = matchingPasses.First();

        var firstUsedTotal =
            CountUsedTrainingPassSessions(firstPass.Bookings) +
            CountSettledSessionDebts(firstPass.SessionDebts);

        if (firstPass.TotalSessions.HasValue &&
            firstUsedTotal >= firstPass.TotalSessions.Value)
        {
            return new TrainingPassBookingCheckResult
            {
                CanUse = false,
                Message = "Član nema preostalih termina u aktivnom paketu."
            };
        }

        return new TrainingPassBookingCheckResult
        {
            CanUse = false,
            Message = "Član je potrošio dozvoljeni broj termina za ovu nedelju."
        };
    }


    [HttpGet("businesses/{businessId:long}/working-hours")]
    public async Task<ActionResult<List<FitnessWorkingHourDto>>> GetBusinessWorkingHours(
    long businessId,
    CancellationToken cancellationToken)
    {
        await EnsureDefaultBusinessWorkingHoursAsync(businessId, cancellationToken);

        var items = await _db.FitnessBusinessWorkingHours
            .AsNoTracking()
            .Where(x => x.BusinessId == businessId)
            .OrderBy(x => x.DayOfWeek)
            .ToListAsync(cancellationToken);

        return Ok(items.Select(MapBusinessWorkingHour).ToList());
    }

    [HttpPut("businesses/{businessId:long}/working-hours")]
    public async Task<ActionResult<List<FitnessWorkingHourDto>>> UpdateBusinessWorkingHours(
        long businessId,
        UpdateFitnessWorkingHoursRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Items.Count == 0)
            return BadRequest("Morate poslati radno vreme za bar jedan dan.");

        var businessExists = await _db.Businesses
            .AnyAsync(x => x.Id == businessId, cancellationToken);

        if (!businessExists)
            return NotFound("Biznis nije pronađen.");

        foreach (var item in request.Items)
        {
            var validationError = ValidateWorkingHourItem(item);
            if (validationError is not null)
                return BadRequest(validationError);
        }

        var existing = await _db.FitnessBusinessWorkingHours
            .Where(x => x.BusinessId == businessId)
            .ToListAsync(cancellationToken);

        foreach (var item in request.Items)
        {
            var entity = existing.FirstOrDefault(x => x.DayOfWeek == item.DayOfWeek);

            if (entity is null)
            {
                entity = new FitnessBusinessWorkingHour
                {
                    BusinessId = businessId,
                    DayOfWeek = item.DayOfWeek
                };

                _db.FitnessBusinessWorkingHours.Add(entity);
            }

            entity.IsClosed = item.IsClosed;
            entity.OpenTime = item.IsClosed ? null : item.OpenTime;
            entity.CloseTime = item.IsClosed ? null : item.CloseTime;
        }

        await _db.SaveChangesAsync(cancellationToken);
        await SyncGlobalBusinessWorkingHoursFromFitnessAsync(businessId, cancellationToken);

        return await GetBusinessWorkingHours(businessId, cancellationToken);
    }

    [HttpGet("rooms/{roomId:long}/working-hours")]
    public async Task<ActionResult<List<FitnessWorkingHourDto>>> GetRoomWorkingHours(
        long roomId,
        CancellationToken cancellationToken)
    {
        var room = await _db.FitnessRooms
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == roomId, cancellationToken);

        if (room is null)
            return NotFound("Sala nije pronađena.");

        await EnsureDefaultRoomWorkingHoursAsync(room.BusinessId, roomId, cancellationToken);

        var items = await _db.FitnessRoomWorkingHours
            .AsNoTracking()
            .Where(x => x.FitnessRoomId == roomId)
            .OrderBy(x => x.DayOfWeek)
            .ToListAsync(cancellationToken);

        return Ok(items.Select(MapRoomWorkingHour).ToList());
    }

    [HttpPut("rooms/{roomId:long}/working-hours")]
    public async Task<ActionResult<List<FitnessWorkingHourDto>>> UpdateRoomWorkingHours(
        long roomId,
        UpdateFitnessWorkingHoursRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Items.Count == 0)
            return BadRequest("Morate poslati radno vreme za bar jedan dan.");

        var room = await _db.FitnessRooms
            .FirstOrDefaultAsync(x => x.Id == roomId, cancellationToken);

        if (room is null)
            return NotFound("Sala nije pronađena.");

        foreach (var item in request.Items)
        {
            var validationError = ValidateWorkingHourItem(item);
            if (validationError is not null)
                return BadRequest(validationError);
        }

        var existing = await _db.FitnessRoomWorkingHours
            .Where(x => x.FitnessRoomId == roomId)
            .ToListAsync(cancellationToken);

        foreach (var item in request.Items)
        {
            var entity = existing.FirstOrDefault(x => x.DayOfWeek == item.DayOfWeek);

            if (entity is null)
            {
                entity = new FitnessRoomWorkingHour
                {
                    BusinessId = room.BusinessId,
                    FitnessRoomId = room.Id,
                    DayOfWeek = item.DayOfWeek
                };

                _db.FitnessRoomWorkingHours.Add(entity);
            }

            entity.IsClosed = item.IsClosed;
            entity.OpenTime = item.IsClosed ? null : item.OpenTime;
            entity.CloseTime = item.IsClosed ? null : item.CloseTime;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return await GetRoomWorkingHours(roomId, cancellationToken);
    }

    [HttpGet("businesses/{businessId:long}/session-templates")]
    public async Task<ActionResult<List<FitnessSessionTemplateDto>>> GetSessionTemplates(
        long businessId,
        CancellationToken cancellationToken)
    {
        var items = await _db.FitnessSessionTemplates
            .AsNoTracking()
            .Include(x => x.FitnessRoom)
            .Include(x => x.FitnessClassType)
            .Include(x => x.TrainerStaffMember)
            .Where(x => x.BusinessId == businessId)
            .OrderBy(x => x.DayOfWeek)
            .ThenBy(x => x.StartTime)
            .ThenBy(x => x.FitnessRoom.DisplayOrder)
            .ThenBy(x => x.FitnessRoom.Name)
            .ToListAsync(cancellationToken);

        return Ok(items.Select(MapSessionTemplate).ToList());
    }

    [HttpPost("businesses/{businessId:long}/session-templates")]
    public async Task<ActionResult<FitnessSessionTemplateDto>> CreateSessionTemplate(
        long businessId,
        CreateFitnessSessionTemplateRequest request,
        CancellationToken cancellationToken)
    {

        var duplicateExists = await _db.FitnessSessionTemplates
    .AnyAsync(x =>
        x.BusinessId == businessId &&
        x.FitnessRoomId == request.FitnessRoomId &&
        x.FitnessClassTypeId == request.FitnessClassTypeId &&
        x.TrainerStaffMemberId == request.TrainerStaffMemberId &&
        x.SessionType == (FitnessSessionType)request.SessionType &&
        x.DayOfWeek == request.DayOfWeek &&
        x.StartTime == request.StartTime &&
        x.DurationMin == request.DurationMin,
        cancellationToken);

        if (duplicateExists)
            return Conflict("Isti šablon termina već postoji.");

        var validationError = await ValidateSessionTemplateRequestAsync(
            businessId,
            request.FitnessRoomId,
            request.FitnessClassTypeId,
            request.SessionType,
            request.DayOfWeek,
            request.StartTime,
            request.DurationMin,
            request.Capacity,
            request.ValidFromDate,
            request.ValidToDate,
            cancellationToken);

        if (validationError is not null)
            return BadRequest(validationError);

        var entity = new FitnessSessionTemplate
        {
            BusinessId = businessId,
            FitnessRoomId = request.FitnessRoomId,
            FitnessClassTypeId = request.FitnessClassTypeId,
            TrainerStaffMemberId = request.TrainerStaffMemberId,
            SessionType = (FitnessSessionType)request.SessionType,
            DayOfWeek = request.DayOfWeek,
            StartTime = request.StartTime,
            DurationMin = request.DurationMin,
            Capacity = request.Capacity,
            IsActive = request.IsActive,
            ValidFromDate = request.ValidFromDate,
            ValidToDate = request.ValidToDate,
            Note = request.Note?.Trim()
        };

        _db.FitnessSessionTemplates.Add(entity);

        await _db.SaveChangesAsync(cancellationToken);

        var created = await _db.FitnessSessionTemplates
            .AsNoTracking()
            .Include(x => x.FitnessRoom)
            .Include(x => x.FitnessClassType)
            .Include(x => x.TrainerStaffMember)
            .FirstAsync(x => x.Id == entity.Id, cancellationToken);

        return Ok(MapSessionTemplate(created));
    }

    [HttpPut("businesses/{businessId:long}/session-templates/{templateId:long}")]
    public async Task<ActionResult<FitnessSessionTemplateDto>> UpdateSessionTemplate(
        long businessId,
        long templateId,
        UpdateFitnessSessionTemplateRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await _db.FitnessSessionTemplates
            .FirstOrDefaultAsync(x => x.Id == templateId && x.BusinessId == businessId, cancellationToken);

        if (entity is null)
            return NotFound("Šablon termina nije pronađen.");

        var duplicateExists = await _db.FitnessSessionTemplates
    .AnyAsync(x =>
        x.Id != templateId &&
        x.BusinessId == businessId &&
        x.FitnessRoomId == request.FitnessRoomId &&
        x.FitnessClassTypeId == request.FitnessClassTypeId &&
        x.TrainerStaffMemberId == request.TrainerStaffMemberId &&
        x.SessionType == (FitnessSessionType)request.SessionType &&
        x.DayOfWeek == request.DayOfWeek &&
        x.StartTime == request.StartTime &&
        x.DurationMin == request.DurationMin,
        cancellationToken);

        if (duplicateExists)
            return Conflict("Isti šablon termina već postoji.");

        var validationError = await ValidateSessionTemplateRequestAsync(
            businessId,
            request.FitnessRoomId,
            request.FitnessClassTypeId,
            request.SessionType,
            request.DayOfWeek,
            request.StartTime,
            request.DurationMin,
            request.Capacity,
            request.ValidFromDate,
            request.ValidToDate,
            cancellationToken);

        if (validationError is not null)
            return BadRequest(validationError);

        entity.FitnessRoomId = request.FitnessRoomId;
        entity.FitnessClassTypeId = request.FitnessClassTypeId;
        entity.TrainerStaffMemberId = request.TrainerStaffMemberId;
        entity.SessionType = (FitnessSessionType)request.SessionType;
        entity.DayOfWeek = request.DayOfWeek;
        entity.StartTime = request.StartTime;
        entity.DurationMin = request.DurationMin;
        entity.Capacity = request.Capacity;
        entity.IsActive = request.IsActive;
        entity.ValidFromDate = request.ValidFromDate;
        entity.ValidToDate = request.ValidToDate;
        entity.Note = request.Note?.Trim();

        await _db.SaveChangesAsync(cancellationToken);

        var updated = await _db.FitnessSessionTemplates
            .AsNoTracking()
            .Include(x => x.FitnessRoom)
            .Include(x => x.FitnessClassType)
            .Include(x => x.TrainerStaffMember)
            .FirstAsync(x => x.Id == entity.Id, cancellationToken);

        return Ok(MapSessionTemplate(updated));
    }

    [HttpPost("businesses/{businessId:long}/sessions/generate")]
    public async Task<ActionResult<GenerateFitnessSessionsResponse>> GenerateSessions(
        long businessId,
        GenerateFitnessSessionsRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ToDate < request.FromDate)
            return BadRequest("Datum do ne može biti pre datuma od.");

        var daysCount = request.ToDate.DayNumber - request.FromDate.DayNumber + 1;

        if (daysCount > 120)
            return BadRequest("Ne možete generisati termine za više od 120 dana odjednom.");

        var businessExists = await _db.Businesses
            .AnyAsync(x => x.Id == businessId, cancellationToken);

        if (!businessExists)
            return NotFound("Biznis nije pronađen.");

        var templates = await _db.FitnessSessionTemplates
            .Include(x => x.FitnessRoom)
            .Where(x => x.BusinessId == businessId && x.IsActive)
            .ToListAsync(cancellationToken);

        var createdCount = 0;
        var skippedExistingCount = 0;
        var skippedInvalidCount = 0;

        for (var date = request.FromDate; date <= request.ToDate; date = date.AddDays(1))
        {
            var dayOfWeek = ToFitnessDayOfWeek(date.DayOfWeek);

            var dayTemplates = templates
                .Where(x => x.DayOfWeek == dayOfWeek)
                .ToList();

            foreach (var template in dayTemplates)
            {
                if (template.ValidFromDate.HasValue && date < template.ValidFromDate.Value)
                {
                    skippedInvalidCount++;
                    continue;
                }

                if (template.ValidToDate.HasValue && date > template.ValidToDate.Value)
                {
                    skippedInvalidCount++;
                    continue;
                }

                var localStart = date.ToDateTime(template.StartTime);
                var localEnd = localStart.AddMinutes(template.DurationMin);

                var startAtUtc = DateTime.SpecifyKind(localStart, DateTimeKind.Local).ToUniversalTime();
                var endAtUtc = DateTime.SpecifyKind(localEnd, DateTimeKind.Local).ToUniversalTime();

                if (endAtUtc <= startAtUtc)
                {
                    skippedInvalidCount++;
                    continue;
                }

                var isInsideWorkingHours = await IsInsideFitnessWorkingHoursAsync(
                    businessId,
                    template.FitnessRoomId,
                    dayOfWeek,
                    template.StartTime,
                    TimeOnly.FromDateTime(localEnd),
                    cancellationToken);

                if (!isInsideWorkingHours)
                {
                    skippedInvalidCount++;
                    continue;
                }

                var existingSession = await _db.FitnessSessions
                    .FirstOrDefaultAsync(
                        x => x.BusinessId == businessId &&
                             x.FitnessRoomId == template.FitnessRoomId &&
                             x.StartAtUtc == startAtUtc &&
                             x.EndAtUtc == endAtUtc,
                        cancellationToken);

                if (existingSession is not null)
                {
                    if (!request.OverwriteExistingGeneratedSessions ||
                        existingSession.FitnessSessionTemplateId is null)
                    {
                        skippedExistingCount++;
                        continue;
                    }

                    existingSession.FitnessSessionTemplateId = template.Id;
                    existingSession.FitnessClassTypeId = template.FitnessClassTypeId;
                    existingSession.TrainerStaffMemberId = template.TrainerStaffMemberId;
                    existingSession.SessionType = template.SessionType;
                    existingSession.Capacity = template.SessionType == FitnessSessionType.Individual
                        ? 1
                        : template.Capacity;
                    existingSession.Status = FitnessSessionStatus.Scheduled;
                    existingSession.Note = template.Note;
                    continue;
                }

                var overlaps = await _db.FitnessSessions
                    .AnyAsync(
                        x => x.BusinessId == businessId &&
                             x.FitnessRoomId == template.FitnessRoomId &&
                             x.Status == FitnessSessionStatus.Scheduled &&
                             x.StartAtUtc < endAtUtc &&
                             startAtUtc < x.EndAtUtc,
                        cancellationToken);

                if (overlaps)
                {
                    skippedExistingCount++;
                    continue;
                }

                var session = new FitnessSession
                {
                    BusinessId = businessId,
                    FitnessRoomId = template.FitnessRoomId,
                    FitnessClassTypeId = template.FitnessClassTypeId,
                    FitnessSessionTemplateId = template.Id,
                    TrainerStaffMemberId = template.TrainerStaffMemberId,
                    SessionType = template.SessionType,
                    StartAtUtc = startAtUtc,
                    EndAtUtc = endAtUtc,
                    Capacity = template.SessionType == FitnessSessionType.Individual
                        ? 1
                        : template.Capacity,
                    Status = FitnessSessionStatus.Scheduled,
                    Note = template.Note
                };

                _db.FitnessSessions.Add(session);
                createdCount++;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new GenerateFitnessSessionsResponse
        {
            CreatedCount = createdCount,
            SkippedExistingCount = skippedExistingCount,
            SkippedInvalidCount = skippedInvalidCount,
            Message = $"Kreirano: {createdCount}, preskočeno postojeće: {skippedExistingCount}, preskočeno neispravno: {skippedInvalidCount}."
        });
    }

    [HttpPost("businesses/{businessId:long}/sessions/generated/delete")]
    public async Task<ActionResult<DeleteGeneratedFitnessSessionsResponse>> DeleteGeneratedSessions(
    long businessId,
    DeleteGeneratedFitnessSessionsRequest request,
    CancellationToken cancellationToken)
    {
        if (request.ToDate < request.FromDate)
            return BadRequest("Datum do ne može biti pre datuma od.");

        var daysCount = request.ToDate.DayNumber - request.FromDate.DayNumber + 1;

        if (daysCount > 180)
            return BadRequest("Ne možete brisati termine za više od 180 dana odjednom.");

        var businessExists = await _db.Businesses
            .AnyAsync(x => x.Id == businessId, cancellationToken);

        if (!businessExists)
            return NotFound("Biznis nije pronađen.");

        var fromLocal = request.FromDate.ToDateTime(TimeOnly.MinValue);
        var toLocalExclusive = request.ToDate.AddDays(1).ToDateTime(TimeOnly.MinValue);

        var fromUtc = DateTime.SpecifyKind(fromLocal, DateTimeKind.Local).ToUniversalTime();
        var toUtcExclusive = DateTime.SpecifyKind(toLocalExclusive, DateTimeKind.Local).ToUniversalTime();

        var sessions = await _db.FitnessSessions
            .Include(x => x.Bookings)
            .Where(x =>
                x.BusinessId == businessId &&
                x.FitnessSessionTemplateId != null &&
                x.StartAtUtc >= fromUtc &&
                x.StartAtUtc < toUtcExclusive)
            .ToListAsync(cancellationToken);

        var deletedSessionsCount = 0;
        var deletedBookingsCount = 0;
        var skippedWithBookingsCount = 0;

        foreach (var session in sessions)
        {
            var hasBookings = session.Bookings.Count > 0;

            if (hasBookings && !request.DeleteSessionsWithBookings)
            {
                skippedWithBookingsCount++;
                continue;
            }

            if (hasBookings)
            {
                deletedBookingsCount += session.Bookings.Count;
                _db.FitnessSessionBookings.RemoveRange(session.Bookings);
            }

            _db.FitnessSessions.Remove(session);
            deletedSessionsCount++;
        }

        await _db.SaveChangesAsync(cancellationToken);

        var message =
            $"Obrisano termina: {deletedSessionsCount}, " +
            $"obrisano prijava: {deletedBookingsCount}, " +
            $"preskočeno termina sa prijavama: {skippedWithBookingsCount}.";

        return Ok(new DeleteGeneratedFitnessSessionsResponse
        {
            DeletedSessionsCount = deletedSessionsCount,
            DeletedBookingsCount = deletedBookingsCount,
            SkippedWithBookingsCount = skippedWithBookingsCount,
            Message = message
        });
    }

    [HttpPost("businesses/{businessId:long}/session-templates/delete")]
    public async Task<ActionResult<DeleteFitnessSessionTemplatesResponse>> DeleteSessionTemplates(
    long businessId,
    DeleteFitnessSessionTemplatesRequest request,
    CancellationToken cancellationToken)
    {
        if (request.TemplateIds.Count == 0)
            return BadRequest("Nije izabran nijedan šablon termina.");

        var templateIds = request.TemplateIds
            .Distinct()
            .ToList();

        var templates = await _db.FitnessSessionTemplates
            .Where(x =>
                x.BusinessId == businessId &&
                templateIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        if (templates.Count == 0)
        {
            return Ok(new DeleteFitnessSessionTemplatesResponse
            {
                DeletedCount = 0,
                SkippedWithGeneratedSessionsCount = 0,
                Message = "Nema šablona za brisanje."
            });
        }

        var templateIdsFound = templates
            .Select(x => x.Id)
            .ToList();

        var templateIdsWithSessions = await _db.FitnessSessions
            .Where(x =>
                x.BusinessId == businessId &&
                x.FitnessSessionTemplateId != null &&
                templateIdsFound.Contains(x.FitnessSessionTemplateId.Value))
            .Select(x => x.FitnessSessionTemplateId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        var deletableTemplates = templates
            .Where(x => !templateIdsWithSessions.Contains(x.Id))
            .ToList();

        var skippedCount = templates.Count - deletableTemplates.Count;

        if (deletableTemplates.Count > 0)
        {
            _db.FitnessSessionTemplates.RemoveRange(deletableTemplates);
            await _db.SaveChangesAsync(cancellationToken);
        }

        var message =
            $"Obrisano šablona: {deletableTemplates.Count}, " +
            $"preskočeno jer imaju generisane termine: {skippedCount}.";

        return Ok(new DeleteFitnessSessionTemplatesResponse
        {
            DeletedCount = deletableTemplates.Count,
            SkippedWithGeneratedSessionsCount = skippedCount,
            Message = message
        });
    }

    private async Task EnsureDefaultBusinessWorkingHoursAsync(
     long businessId,
     CancellationToken cancellationToken)
    {
        var businessExists = await _db.Businesses
            .AnyAsync(x => x.Id == businessId, cancellationToken);

        if (!businessExists)
            return;

        var existingDays = await _db.FitnessBusinessWorkingHours
            .Where(x => x.BusinessId == businessId)
            .Select(x => x.DayOfWeek)
            .ToListAsync(cancellationToken);

        for (var day = 1; day <= 7; day++)
        {
            if (existingDays.Contains(day))
                continue;

            _db.FitnessBusinessWorkingHours.Add(new FitnessBusinessWorkingHour
            {
                BusinessId = businessId,
                DayOfWeek = day,
                IsClosed = day == 7,
                OpenTime = day == 7 ? null : new TimeOnly(8, 0),
                CloseTime = day == 7 ? null : new TimeOnly(22, 0)
            });
        }

        await _db.SaveChangesAsync(cancellationToken);

        await SyncGlobalBusinessWorkingHoursFromFitnessAsync(
            businessId,
            cancellationToken);
    }

    private async Task EnsureDefaultRoomWorkingHoursAsync(
        long businessId,
        long roomId,
        CancellationToken cancellationToken)
    {
        var existingDays = await _db.FitnessRoomWorkingHours
            .Where(x => x.FitnessRoomId == roomId)
            .Select(x => x.DayOfWeek)
            .ToListAsync(cancellationToken);

        var businessHours = await _db.FitnessBusinessWorkingHours
            .AsNoTracking()
            .Where(x => x.BusinessId == businessId)
            .ToListAsync(cancellationToken);

        if (businessHours.Count == 0)
        {
            await EnsureDefaultBusinessWorkingHoursAsync(businessId, cancellationToken);

            businessHours = await _db.FitnessBusinessWorkingHours
                .AsNoTracking()
                .Where(x => x.BusinessId == businessId)
                .ToListAsync(cancellationToken);
        }

        for (var day = 1; day <= 7; day++)
        {
            if (existingDays.Contains(day))
                continue;

            var businessDay = businessHours.FirstOrDefault(x => x.DayOfWeek == day);

            _db.FitnessRoomWorkingHours.Add(new FitnessRoomWorkingHour
            {
                BusinessId = businessId,
                FitnessRoomId = roomId,
                DayOfWeek = day,
                IsClosed = businessDay?.IsClosed ?? day == 7,
                OpenTime = businessDay?.OpenTime,
                CloseTime = businessDay?.CloseTime
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static string? ValidateWorkingHourItem(UpdateFitnessWorkingHourItemRequest item)
    {
        if (item.DayOfWeek is < 1 or > 7)
            return "Dan u nedelji mora biti od 1 do 7.";

        if (item.IsClosed)
            return null;

        if (!item.OpenTime.HasValue)
            return "Za radni dan morate uneti vreme otvaranja.";

        if (!item.CloseTime.HasValue)
            return "Za radni dan morate uneti vreme zatvaranja.";

        if (item.CloseTime <= item.OpenTime)
            return "Vreme zatvaranja mora biti posle vremena otvaranja.";

        return null;
    }

    private async Task<string?> ValidateSessionTemplateRequestAsync(
        long businessId,
        long fitnessRoomId,
        long? fitnessClassTypeId,
        int sessionType,
        int dayOfWeek,
        TimeOnly startTime,
        int durationMin,
        int capacity,
        DateOnly? validFromDate,
        DateOnly? validToDate,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(typeof(FitnessSessionType), sessionType))
            return "Vrsta termina nije ispravna.";

        if (dayOfWeek is < 1 or > 7)
            return "Dan u nedelji mora biti od 1 do 7.";

        if (durationMin <= 0)
            return "Trajanje termina mora biti veće od 0 minuta.";

        if (capacity <= 0)
            return "Kapacitet mora biti veći od 0.";

        if (validFromDate.HasValue &&
            validToDate.HasValue &&
            validToDate.Value < validFromDate.Value)
        {
            return "Datum važenja do ne može biti pre datuma važenja od.";
        }

        var room = await _db.FitnessRooms
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == fitnessRoomId &&
                     x.BusinessId == businessId,
                cancellationToken);

        if (room is null)
            return "Sala nije pronađena.";

        var parsedSessionType = (FitnessSessionType)sessionType;

        if (parsedSessionType == FitnessSessionType.Group && !room.AllowsGroupClasses)
            return "Izabrana sala ne dozvoljava grupne treninge.";

        if (parsedSessionType == FitnessSessionType.Individual && !room.AllowsIndividualTraining)
            return "Izabrana sala ne dozvoljava individualne treninge.";

        if (parsedSessionType == FitnessSessionType.Individual)
        {
            capacity = 1;
        }

        if (capacity > room.Capacity)
            return $"Kapacitet ne može biti veći od kapaciteta sale ({room.Capacity}).";

        if (fitnessClassTypeId.HasValue)
        {
            var classTypeExists = await _db.FitnessClassTypes
                .AsNoTracking()
                .AnyAsync(
                    x => x.Id == fitnessClassTypeId.Value &&
                         x.BusinessId == businessId,
                    cancellationToken);

            if (!classTypeExists)
                return "Tip treninga nije pronađen.";
        }

        var endTime = startTime.AddMinutes(durationMin);

        var insideWorkingHours = await IsInsideFitnessWorkingHoursAsync(
            businessId,
            fitnessRoomId,
            dayOfWeek,
            startTime,
            endTime,
            cancellationToken);

        if (!insideWorkingHours)
            return "Šablon termina mora biti u okviru radnog vremena biznisa i sale.";

        return null;
    }

    private async Task<bool> IsInsideFitnessWorkingHoursAsync(
        long businessId,
        long roomId,
        int dayOfWeek,
        TimeOnly startTime,
        TimeOnly endTime,
        CancellationToken cancellationToken)
    {
        await EnsureDefaultBusinessWorkingHoursAsync(businessId, cancellationToken);
        await EnsureDefaultRoomWorkingHoursAsync(businessId, roomId, cancellationToken);

        var businessHour = await _db.FitnessBusinessWorkingHours
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.BusinessId == businessId &&
                     x.DayOfWeek == dayOfWeek,
                cancellationToken);

        if (businessHour is null ||
            businessHour.IsClosed ||
            !businessHour.OpenTime.HasValue ||
            !businessHour.CloseTime.HasValue)
        {
            return false;
        }

        var roomHour = await _db.FitnessRoomWorkingHours
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.FitnessRoomId == roomId &&
                     x.DayOfWeek == dayOfWeek,
                cancellationToken);

        if (roomHour is null ||
            roomHour.IsClosed ||
            !roomHour.OpenTime.HasValue ||
            !roomHour.CloseTime.HasValue)
        {
            return false;
        }

        return startTime >= businessHour.OpenTime.Value &&
               endTime <= businessHour.CloseTime.Value &&
               startTime >= roomHour.OpenTime.Value &&
               endTime <= roomHour.CloseTime.Value;
    }

    private static FitnessWorkingHourDto MapBusinessWorkingHour(FitnessBusinessWorkingHour entity)
    {
        return new FitnessWorkingHourDto
        {
            Id = entity.Id,
            BusinessId = entity.BusinessId,
            FitnessRoomId = null,
            DayOfWeek = entity.DayOfWeek,
            DayOfWeekText = GetDayOfWeekText(entity.DayOfWeek),
            IsClosed = entity.IsClosed,
            OpenTime = entity.OpenTime,
            CloseTime = entity.CloseTime,
            TimeText = GetWorkingHourText(entity.IsClosed, entity.OpenTime, entity.CloseTime)
        };
    }

    private static FitnessWorkingHourDto MapRoomWorkingHour(FitnessRoomWorkingHour entity)
    {
        return new FitnessWorkingHourDto
        {
            Id = entity.Id,
            BusinessId = entity.BusinessId,
            FitnessRoomId = entity.FitnessRoomId,
            DayOfWeek = entity.DayOfWeek,
            DayOfWeekText = GetDayOfWeekText(entity.DayOfWeek),
            IsClosed = entity.IsClosed,
            OpenTime = entity.OpenTime,
            CloseTime = entity.CloseTime,
            TimeText = GetWorkingHourText(entity.IsClosed, entity.OpenTime, entity.CloseTime)
        };
    }

    private static string GetFitnessSessionTypeText(FitnessSessionType sessionType)
    {
        return sessionType switch
        {
            FitnessSessionType.Group => "Grupni",
            FitnessSessionType.Individual => "Individualni",
            _ => sessionType.ToString()
        };
    }

    private static FitnessSessionTemplateDto MapSessionTemplate(FitnessSessionTemplate entity)
    {
        var endTime = entity.StartTime.AddMinutes(entity.DurationMin);

        var roomName = entity.FitnessRoom?.Name ?? $"Sala #{entity.FitnessRoomId}";
        var className = entity.FitnessClassType?.Name;

        var displayText =
            $"{GetDayOfWeekText(entity.DayOfWeek)} {entity.StartTime:HH\\:mm}-{endTime:HH\\:mm} | " +
            $"{roomName} | " +
            $"{(string.IsNullOrWhiteSpace(className) ? "-" : className)} | " +
            $"{GetFitnessSessionTypeText(entity.SessionType)} | " +
            $"{entity.Capacity} mesta";

        return new FitnessSessionTemplateDto
        {
            Id = entity.Id,
            BusinessId = entity.BusinessId,
            FitnessRoomId = entity.FitnessRoomId,
            FitnessRoomName = roomName,
            FitnessClassTypeId = entity.FitnessClassTypeId,
            FitnessClassTypeName = className,
            TrainerStaffMemberId = entity.TrainerStaffMemberId,
            TrainerName = null,
            SessionType = (int)entity.SessionType,
            SessionTypeText = GetFitnessSessionTypeText(entity.SessionType),
            DayOfWeek = entity.DayOfWeek,
            DayOfWeekText = GetDayOfWeekText(entity.DayOfWeek),
            StartTime = entity.StartTime,
            DurationMin = entity.DurationMin,
            EndTime = endTime,
            Capacity = entity.Capacity,
            IsActive = entity.IsActive,
            ValidFromDate = entity.ValidFromDate,
            ValidToDate = entity.ValidToDate,
            Note = entity.Note,
            DisplayText = displayText
        };
    }

    private static string GetWorkingHourText(
        bool isClosed,
        TimeOnly? openTime,
        TimeOnly? closeTime)
    {
        if (isClosed)
            return "Zatvoreno";

        if (!openTime.HasValue || !closeTime.HasValue)
            return "-";

        return $"{openTime.Value:HH\\:mm} - {closeTime.Value:HH\\:mm}";
    }

    private static int ToFitnessDayOfWeek(DayOfWeek dayOfWeek)
    {
        return dayOfWeek switch
        {
            DayOfWeek.Monday => 1,
            DayOfWeek.Tuesday => 2,
            DayOfWeek.Wednesday => 3,
            DayOfWeek.Thursday => 4,
            DayOfWeek.Friday => 5,
            DayOfWeek.Saturday => 6,
            DayOfWeek.Sunday => 7,
            _ => 1
        };
    }

    private static string GetDayOfWeekText(int dayOfWeek)
    {
        return dayOfWeek switch
        {
            1 => "Ponedeljak",
            2 => "Utorak",
            3 => "Sreda",
            4 => "Četvrtak",
            5 => "Petak",
            6 => "Subota",
            7 => "Nedelja",
            _ => dayOfWeek.ToString()
        };
    }

    private static int CountActiveBookings(IEnumerable<FitnessSessionBooking> bookings)
    {
        return bookings.Count(x =>
            x.Status == FitnessSessionBookingStatus.Booked ||
            x.Status == FitnessSessionBookingStatus.Attended);
    }

    private static int CountReportActiveBookings(IEnumerable<FitnessSessionBooking> bookings)
    {
        return bookings.Count(x =>
            x.Status == FitnessSessionBookingStatus.Booked ||
            x.Status == FitnessSessionBookingStatus.Attended ||
            x.Status == FitnessSessionBookingStatus.PendingApproval);
    }

    private static int CountUsedTrainingPassSessions(IEnumerable<FitnessSessionBooking> bookings)
    {
        return bookings.Count(x =>
            x.ConsumesTrainingPassSession &&
            x.Status != FitnessSessionBookingStatus.CancelledByBusiness &&
            x.Status != FitnessSessionBookingStatus.CancelledByCustomer &&
            x.Status != FitnessSessionBookingStatus.Rejected);
    }

    private static int CountSettledSessionDebts(IEnumerable<FitnessMemberSessionDebt> debts)
    {
        return debts
            .Where(x => x.Status == FitnessMemberSessionDebtStatus.Settled)
            .Sum(x => x.SessionsCount);
    }

    private static int CountUsedTrainingPassSessionsInPeriod(
        IEnumerable<FitnessSessionBooking> bookings,
        DateOnly fromDate,
        DateOnly toDate)
    {
        return bookings.Count(x =>
            x.ConsumesTrainingPassSession &&
            x.Status != FitnessSessionBookingStatus.CancelledByBusiness &&
            x.Status != FitnessSessionBookingStatus.CancelledByCustomer &&
            x.Status != FitnessSessionBookingStatus.Rejected &&
            DateOnly.FromDateTime(x.CreatedAtUtc) >= fromDate &&
            DateOnly.FromDateTime(x.CreatedAtUtc) <= toDate);
    }

    private static int CountUsedTrainingPassSessionsInSessionPeriod(
    IEnumerable<FitnessSessionBooking> bookings,
    DateOnly fromDate,
    DateOnly toDate)
    {
        return bookings.Count(x =>
            x.ConsumesTrainingPassSession &&
            x.Status != FitnessSessionBookingStatus.CancelledByBusiness &&
            x.Status != FitnessSessionBookingStatus.CancelledByCustomer &&
            x.Status != FitnessSessionBookingStatus.Rejected &&
            x.FitnessSession != null &&
            DateOnly.FromDateTime(x.FitnessSession.StartAtUtc.ToLocalTime()) >= fromDate &&
            DateOnly.FromDateTime(x.FitnessSession.StartAtUtc.ToLocalTime()) <= toDate);
    }

    private static DateOnly GetWeekStart(DateOnly date)
    {
        var diff = date.DayOfWeek switch
        {
            DayOfWeek.Monday => 0,
            DayOfWeek.Tuesday => 1,
            DayOfWeek.Wednesday => 2,
            DayOfWeek.Thursday => 3,
            DayOfWeek.Friday => 4,
            DayOfWeek.Saturday => 5,
            DayOfWeek.Sunday => 6,
            _ => 0
        };

        return date.AddDays(-diff);
    }

    private static string? NormalizeNullableText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private sealed class TrainingPassBookingCheckResult
    {
        public bool CanUse { get; set; }

        public string Message { get; set; } = string.Empty;

        public FitnessMemberTrainingPass? TrainingPass { get; set; }
    }

    private static FitnessMemberSessionDebtDto ToSessionDebtDto(FitnessMemberSessionDebt debt)
    {
        var sessionStartLocal = debt.FitnessSession.StartAtUtc.ToLocalTime();
        var sessionEndLocal = debt.FitnessSession.EndAtUtc.ToLocalTime();

        return new FitnessMemberSessionDebtDto
        {
            Id = debt.Id,
            BusinessId = debt.BusinessId,
            FitnessMemberId = debt.FitnessMemberId,
            FitnessMemberName = debt.FitnessMember?.FullName ?? string.Empty,
            FitnessMemberPhone = debt.FitnessMember?.Phone ?? string.Empty,
            FitnessSessionId = debt.FitnessSessionId,
            FitnessClassTypeId = debt.FitnessClassTypeId,
            FitnessClassTypeName = debt.FitnessClassType?.Name ?? string.Empty,
            FitnessMemberTrainingPassId = debt.FitnessMemberTrainingPassId,
            TrainingPassText = debt.FitnessMemberTrainingPass is null
                ? "-"
                : debt.FitnessMemberTrainingPass.PlanNameSnapshot,
            SessionStartAtUtc = debt.FitnessSession.StartAtUtc,
            SessionDateText = sessionStartLocal.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
            SessionTimeText = $"{sessionStartLocal:HH:mm} - {sessionEndLocal:HH:mm}",
            RoomName = debt.FitnessSession.FitnessRoom?.Name ?? string.Empty,
            SessionsCount = debt.SessionsCount,
            Status = (int)debt.Status,
            StatusText = GetSessionDebtStatusText(debt.Status),
            SettledAtUtc = debt.SettledAtUtc,
            VoidedAtUtc = debt.VoidedAtUtc,
            VoidReason = debt.VoidReason,
            Note = debt.Note,
            CreatedAtUtc = debt.CreatedAtUtc,
            UpdatedAtUtc = debt.UpdatedAtUtc
        };
    }

    private static string GetSessionDebtStatusText(FitnessMemberSessionDebtStatus status)
    {
        return status switch
        {
            FitnessMemberSessionDebtStatus.Open => "Otvoren",
            FitnessMemberSessionDebtStatus.Settled => "Prebijen uplatom",
            FitnessMemberSessionDebtStatus.Voided => "Storniran",
            _ => "Nepoznato"
        };
    }

    private static FitnessRoomDto ToRoomDto(FitnessRoom room)
    {
        return new FitnessRoomDto
        {
            Id = room.Id,
            BusinessId = room.BusinessId,
            Name = room.Name,
            Capacity = room.Capacity,
            IsActive = room.IsActive,
            AllowsGroupClasses = room.AllowsGroupClasses,
            AllowsIndividualTraining = room.AllowsIndividualTraining,
            DisplayOrder = room.DisplayOrder,
            CreatedAtUtc = room.CreatedAtUtc,
            UpdatedAtUtc = room.UpdatedAtUtc
        };
    }

    private static FitnessClassTypeDto ToClassTypeDto(FitnessClassType item)
    {
        return new FitnessClassTypeDto
        {
            Id = item.Id,
            BusinessId = item.BusinessId,
            Name = item.Name,
            Description = item.Description,
            DefaultDurationMin = item.DefaultDurationMin,
            DefaultCapacity = item.DefaultCapacity,
            IsActive = item.IsActive,
            DisplayOrder = item.DisplayOrder,
            CreatedAtUtc = item.CreatedAtUtc,
            UpdatedAtUtc = item.UpdatedAtUtc
        };
    }

    private static FitnessSettingsDto ToSettingsDto(FitnessSettings settings)
    {
        return new FitnessSettingsDto
        {
            Id = settings.Id,
            BusinessId = settings.BusinessId,
            GroupClassesEnabled = settings.GroupClassesEnabled,
            IndividualTrainingEnabled = settings.IndividualTrainingEnabled,
            MembershipsEnabled = settings.MembershipsEnabled,
            UnpaidMembershipBookingPolicy = (int)settings.UnpaidMembershipBookingPolicy,
            ReceivesCustomerMessages = settings.ReceivesCustomerMessages,
            UnpaidMembershipBookingPolicyText = GetUnpaidMembershipBookingPolicyText(
                settings.UnpaidMembershipBookingPolicy),
            DefaultMembershipDurationDays = settings.DefaultMembershipDurationDays,
            AllowCustomerCancelBooking = settings.AllowCustomerCancelBooking,
            CustomerCancelDeadlineMinutes = settings.CustomerCancelDeadlineMinutes,
            CreatedAtUtc = settings.CreatedAtUtc,
            UpdatedAtUtc = settings.UpdatedAtUtc
        };
    }

    private static FitnessSessionDto ToSessionDto(FitnessSession session)
    {
        var bookedCount = CountActiveBookings(session.Bookings);

        return new FitnessSessionDto
        {
            Id = session.Id,
            BusinessId = session.BusinessId,
            FitnessRoomId = session.FitnessRoomId,
            FitnessRoomName = session.FitnessRoom?.Name ?? string.Empty,
            FitnessClassTypeId = session.FitnessClassTypeId,
            FitnessClassTypeName = session.FitnessClassType?.Name,
            TrainerStaffMemberId = session.TrainerStaffMemberId,
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
                bookedCount < session.Capacity,
            Status = (int)session.Status,
            StatusText = GetSessionStatusText(session.Status),
            Note = session.Note,
            CreatedAtUtc = session.CreatedAtUtc,
            UpdatedAtUtc = session.UpdatedAtUtc
        };
    }

    private static FitnessSessionBookingDto ToBookingDto(FitnessSessionBooking booking)
    {
        return new FitnessSessionBookingDto
        {
            Id = booking.Id,
            BusinessId = booking.BusinessId,
            FitnessSessionId = booking.FitnessSessionId,
            FitnessMemberId = booking.FitnessMemberId,
            CustomerProfileId = booking.CustomerProfileId,
            BusinessCustomerId = booking.BusinessCustomerId,
            AppUserId = booking.AppUserId,
            FitnessMemberTrainingPassId = booking.FitnessMemberTrainingPassId,
            ConsumesTrainingPassSession = booking.ConsumesTrainingPassSession,
            CustomerName = booking.CustomerName,
            CustomerPhone = booking.CustomerPhone,
            Status = (int)booking.Status,
            StatusText = GetBookingStatusText(booking.Status),
            MembershipWasActiveAtBooking = booking.MembershipWasActiveAtBooking,
            MembershipWarningText = booking.MembershipWarningText,
            CreatedAtUtc = booking.CreatedAtUtc,
            CancelledAtUtc = booking.CancelledAtUtc,
            AttendedAtUtc = booking.AttendedAtUtc,
            NoShowAtUtc = booking.NoShowAtUtc,
            UpdatedAtUtc = booking.UpdatedAtUtc
        };
    }

    private static FitnessMemberDto ToMemberDto(FitnessMember member)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var activePayment = member.Payments
            .Where(x => x.PeriodStartDate <= today && x.PeriodEndDate >= today)
            .OrderByDescending(x => x.PeriodEndDate)
            .FirstOrDefault();

        var latestPayment = member.Payments
            .OrderByDescending(x => x.PeriodEndDate)
            .FirstOrDefault();

        var hasActiveMembership = activePayment is not null;
        var activeUntil = activePayment?.PeriodEndDate;

        var statusText = hasActiveMembership
            ? $"Aktivna do {activeUntil:dd.MM.yyyy}"
            : latestPayment is null
                ? "Nema evidentiranu članarinu"
                : $"Istekla {latestPayment.PeriodEndDate:dd.MM.yyyy}";

        return new FitnessMemberDto
        {
            Id = member.Id,
            BusinessId = member.BusinessId,
            CustomerProfileId = member.CustomerProfileId,
            BusinessCustomerId = member.BusinessCustomerId,
            AppUserId = member.AppUserId,
            FullName = member.FullName,
            Phone = member.Phone,
            Email = member.Email,
            MemberCode = member.MemberCode,
            IsActive = member.IsActive,
            HasActiveMembership = hasActiveMembership,
            ActiveMembershipUntil = activeUntil,
            MembershipStatusText = statusText,
            CreatedAtUtc = member.CreatedAtUtc,
            UpdatedAtUtc = member.UpdatedAtUtc
        };
    }

    private static FitnessMembershipPlanDto ToMembershipPlanDto(FitnessMembershipPlan plan)
    {
        var classTypeName = plan.FitnessClassType?.Name ?? string.Empty;

        var totalSessionsText = plan.TotalSessions.HasValue
            ? $"{plan.TotalSessions.Value} termina"
            : "Neograničeno";

        var weeklyLimitText = plan.WeeklySessionLimit.HasValue
            ? $"{plan.WeeklySessionLimit.Value} nedeljno"
            : "Bez nedeljnog limita";

        var carryOverText = plan.UnusedSessionsCarryOver
            ? "Prenosi se"
            : "Ne prenosi se";

        var displayText =
            $"{plan.Name} | {totalSessionsText} | {plan.DefaultValidityDays} dana | {plan.Price:N2} {plan.Currency}";

        return new FitnessMembershipPlanDto
        {
            Id = plan.Id,
            BusinessId = plan.BusinessId,
            FitnessClassTypeId = plan.FitnessClassTypeId,
            FitnessClassTypeName = classTypeName,
            Name = plan.Name,
            TotalSessions = plan.TotalSessions,
            TotalSessionsText = totalSessionsText,
            WeeklySessionLimit = plan.WeeklySessionLimit,
            WeeklySessionLimitText = weeklyLimitText,
            DefaultValidityDays = plan.DefaultValidityDays,
            Price = plan.Price,
            Currency = plan.Currency,
            UnusedSessionsCarryOver = plan.UnusedSessionsCarryOver,
            CarryOverText = carryOverText,
            IsActive = plan.IsActive,
            IsActiveText = plan.IsActive ? "Da" : "Ne",
            DisplayOrder = plan.DisplayOrder,
            Note = plan.Note,
            DisplayText = displayText
        };
    }

    private static FitnessMemberTrainingPassDto ToMemberTrainingPassDto(FitnessMemberTrainingPass pass)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var usedSessions =
            CountUsedTrainingPassSessions(pass.Bookings) +
            CountSettledSessionDebts(pass.SessionDebts);

        int? remainingSessions = pass.TotalSessions.HasValue
            ? Math.Max(0, pass.TotalSessions.Value - usedSessions)
            : null;

        var weekStart = GetWeekStart(today);
        var weekEnd = weekStart.AddDays(6);

        var usedThisWeek = CountUsedTrainingPassSessionsInPeriod(
            pass.Bookings,
            weekStart,
            weekEnd);

        var sessionsText = pass.TotalSessions.HasValue
            ? $"{usedSessions}/{pass.TotalSessions.Value} potrošeno, ostalo {remainingSessions}"
            : $"{usedSessions} potrošeno, neograničeno";

        var weeklyLimitText = pass.WeeklySessionLimit.HasValue
            ? $"{usedThisWeek}/{pass.WeeklySessionLimit.Value} ove nedelje"
            : "Bez nedeljnog limita";

        var isCurrentlyValid =
    !pass.IsVoided &&
    pass.IsActive &&
    pass.ValidFromDate <= today &&
    pass.ValidToDate >= today;

        var currentStatusText = pass.IsVoided
            ? $"Storniran {pass.VoidedAtUtc?.ToLocalTime():dd.MM.yyyy}"
            : isCurrentlyValid
                ? "Aktivan"
                : !pass.IsActive
                    ? "Neaktivan"
                    : today < pass.ValidFromDate
                        ? $"Počinje {pass.ValidFromDate:dd.MM.yyyy}"
                        : $"Istekao {pass.ValidToDate:dd.MM.yyyy}";

        var displayText =
            $"{pass.PlanNameSnapshot} | {pass.ValidFromDate:dd.MM.yyyy} - {pass.ValidToDate:dd.MM.yyyy} | {sessionsText}";

        return new FitnessMemberTrainingPassDto
        {
            Id = pass.Id,
            BusinessId = pass.BusinessId,
            FitnessMemberId = pass.FitnessMemberId,
            FitnessMemberName = pass.FitnessMember?.FullName ?? string.Empty,
            FitnessMemberPhone = pass.FitnessMember?.Phone ?? string.Empty,
            FitnessMembershipPlanId = pass.FitnessMembershipPlanId,
            FitnessClassTypeId = pass.FitnessClassTypeId,
            PlanNameSnapshot = pass.PlanNameSnapshot,
            FitnessClassTypeNameSnapshot = pass.FitnessClassTypeNameSnapshot,
            ValidFromDate = pass.ValidFromDate,
            ValidToDate = pass.ValidToDate,
            ValidPeriodText = $"{pass.ValidFromDate:dd.MM.yyyy} - {pass.ValidToDate:dd.MM.yyyy}",
            TotalSessions = pass.TotalSessions,
            UsedSessions = usedSessions,
            RemainingSessions = remainingSessions,
            SessionsText = sessionsText,
            WeeklySessionLimit = pass.WeeklySessionLimit,
            UsedThisWeek = usedThisWeek,
            WeeklyLimitText = weeklyLimitText,
            PricePaid = pass.PricePaid,
            Currency = pass.Currency,
            PaidAtUtc = pass.PaidAtUtc,
            IsActive = pass.IsActive,
            IsActiveText = pass.IsActive ? "Da" : "Ne",
            IsCurrentlyValid = isCurrentlyValid,
            CurrentStatusText = currentStatusText,
            Note = pass.Note,
            DisplayText = displayText,
            IsVoided = pass.IsVoided,
            VoidedAtUtc = pass.VoidedAtUtc,
            VoidReason = pass.VoidReason,
            VoidStatusText = pass.IsVoided
    ? $"Storniran: {pass.VoidReason}"
    : "Nije storniran",
        };
    }

    private static FitnessMembershipPaymentDto ToPaymentDto(FitnessMembershipPayment payment)
    {
        return new FitnessMembershipPaymentDto
        {
            Id = payment.Id,
            BusinessId = payment.BusinessId,
            FitnessMemberId = payment.FitnessMemberId,
            FitnessMemberName = payment.FitnessMember?.FullName ?? string.Empty,
            Amount = payment.Amount,
            Currency = payment.Currency,
            PeriodStartDate = payment.PeriodStartDate,
            PeriodEndDate = payment.PeriodEndDate,
            PaidAtUtc = payment.PaidAtUtc,
            PaymentMethod = payment.PaymentMethod,
            Note = payment.Note,
            CreatedByUserId = payment.CreatedByUserId,
            CreatedByUserName = null,
            CreatedAtUtc = payment.CreatedAtUtc,
            UpdatedAtUtc = payment.UpdatedAtUtc
        };
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

    private static string GetSessionStatusText(FitnessSessionStatus status)
    {
        return status switch
        {
            FitnessSessionStatus.Scheduled => "Zakazan",
            FitnessSessionStatus.Cancelled => "Otkazan",
            FitnessSessionStatus.Completed => "Završen",
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

    private async Task SyncGlobalBusinessWorkingHoursFromFitnessAsync(
     long businessId,
     CancellationToken cancellationToken)
    {
        var fitnessHours = await _db.FitnessBusinessWorkingHours
            .AsNoTracking()
            .Where(x => x.BusinessId == businessId)
            .OrderBy(x => x.DayOfWeek)
            .ToListAsync(cancellationToken);

        if (fitnessHours.Count == 0)
            return;

        var globalHours = await _db.BusinessWorkingHours
            .Where(x => x.BusinessId == businessId)
            .ToListAsync(cancellationToken);

        foreach (var fitnessHour in fitnessHours)
        {
            var globalHour = globalHours
                .FirstOrDefault(x => x.DayOfWeek == fitnessHour.DayOfWeek);

            if (globalHour is null)
            {
                globalHour = new BusinessWorkingHour
                {
                    BusinessId = businessId,
                    DayOfWeek = fitnessHour.DayOfWeek
                };

                _db.BusinessWorkingHours.Add(globalHour);
            }

            globalHour.IsClosed = fitnessHour.IsClosed;

            if (fitnessHour.IsClosed)
            {
                globalHour.StartTime = TimeSpan.Zero;
                globalHour.EndTime = TimeSpan.Zero;
            }
            else
            {
                globalHour.StartTime = fitnessHour.OpenTime?.ToTimeSpan() ?? TimeSpan.Zero;
                globalHour.EndTime = fitnessHour.CloseTime?.ToTimeSpan() ?? TimeSpan.Zero;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static string GetUnpaidMembershipBookingPolicyText(
        FitnessUnpaidMembershipBookingPolicy policy)
    {
        return policy switch
        {
            FitnessUnpaidMembershipBookingPolicy.Block => "Ne dozvoli prijavu",
            FitnessUnpaidMembershipBookingPolicy.Allow => "Dozvoli prijavu",
            FitnessUnpaidMembershipBookingPolicy.AllowWithNotification => "Dozvoli prijavu uz upozorenje",
            FitnessUnpaidMembershipBookingPolicy.RequireApproval => "Prijava ide na odobrenje",
            _ => "Nepoznato"
        };
    }
}