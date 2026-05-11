using BookingPlatform.Domain.Chat;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingPlatform.Infrastructure.Persistence.Configurations;

public sealed class ChatConversationConfiguration : IEntityTypeConfiguration<ChatConversation>
{
    public void Configure(EntityTypeBuilder<ChatConversation> builder)
    {
        builder.ToTable("chat_conversations");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.LastMessageText)
            .HasMaxLength(500);

        builder.Property(x => x.IsActive)
            .HasDefaultValue(true);

        builder.HasIndex(x => x.BusinessId);

        builder.HasIndex(x => x.BusinessCustomerId);

        builder.HasIndex(x => new { x.BusinessId, x.BusinessCustomerId })
            .IsUnique()
            .HasFilter("\"BusinessCustomerId\" IS NOT NULL");

        builder.HasIndex(x => x.LastMessageAtUtc);
    }
}