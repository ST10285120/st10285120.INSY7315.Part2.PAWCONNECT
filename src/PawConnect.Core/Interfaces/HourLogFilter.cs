using PawConnect.Core.Entities;
using PawConnect.Core.Enums;

namespace PawConnect.Core.Interfaces;

public record HourLogFilter(Guid? VolunteerId = null, HourLogStatus? Status = null);
