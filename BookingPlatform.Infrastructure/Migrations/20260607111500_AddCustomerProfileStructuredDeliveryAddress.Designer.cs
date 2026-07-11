using BookingPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookingPlatform.Infrastructure.Migrations
{
    [DbContext(typeof(BookingDbContext))]
    [Migration("20260607111500_AddCustomerProfileStructuredDeliveryAddress")]
    partial class AddCustomerProfileStructuredDeliveryAddress
    {
    }
}
