using FinalProject_PRN222_Group7.DAL.Enums;

namespace FinalProject_PRN222_Group7.DAL.Entities;

public class Package
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public PackageType Type { get; set; }
    public decimal Price { get; set; }
    public int MaxQuestionsPerDay { get; set; }
    public int DurationDays { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
