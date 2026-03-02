namespace Domain.Entities
{
    public abstract class BaseEntity
    {
        public Guid Id { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }

        protected BaseEntity()
        {
            Id = Guid.NewGuid();
            var now = DateTime.UtcNow;
            CreatedAt = now;
            UpdatedAt = now;
        }

        public void UpdateTimestamp()
        {
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
