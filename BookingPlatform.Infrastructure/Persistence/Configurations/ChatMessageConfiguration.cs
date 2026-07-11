using BookingPlatform.Domain.Chat;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingPlatform.Infrastructure.Persistence.Configurations;

public sealed class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.ToTable("chat_messages");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Text)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(x => x.SenderType)
            .HasConversion<int>();

        builder.HasIndex(x => x.ConversationId);

        builder.HasIndex(x => x.RestaurantTableReservationId);

        builder.HasIndex(x => x.RestaurantOrderId);

        builder.HasIndex(x => x.SharedRestaurantOrderId);

        builder.HasIndex(x => new { x.ConversationId, x.CreatedAtUtc });
    }
}
