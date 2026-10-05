namespace PawConnect.Core.Entities;

/// <summary>
/// A shelter location. The shelter has one branch today, but the Scalability NFR requires
/// growth to three branches without redesigning the data model.
/// </summary>
public class ShelterBranch
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Location { get; set; } = "";
    public bool IsActive { get; set; } = true;
}
