using BookingPlatform.Domain.Chat;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingPlatform.Infrastructure.Persistence.Configurations;

public sealed class ChatConversationMemberConfiguration : IEntityTypeConfiguration<ChatConversationMember>
{
    public void Configure(EntityTypeBuilder<ChatConversationMember> builder)
    {
        builder.ToTable("chat_conversation_members");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.DisplayNameSnapshot)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.IsActive)
            .HasDefaultValue(true);

        builder.HasIndex(x => x.ConversationId);

        builder.HasIndex(x => x.CustomerProfileId);

        builder.HasIndex(x => x.AppUserId);

        builder.HasIndex(x => new { x.ConversationId, x.CustomerProfileId })
            .IsUnique();

        builder.HasOne<ChatConversation>()
            .WithMany()
            .HasForeignKey(x => x.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
