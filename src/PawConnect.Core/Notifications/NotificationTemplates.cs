using PawConnect.Core.Entities;
using PawConnect.Core.Common;

namespace PawConnect.Core.Notifications;

/// <summary>Wording for every message PawConnect sends, kept in one place.</summary>
public static class NotificationTemplates
{
    public static NotificationRequest ApplicationReceived(AdoptionApplication app, string animalName) => new(
        $"We've received your application for {animalName} ({app.ReferenceNumber})",
        $"Hi {FirstName(app.FullName)},\n\nThank you for applying to adopt {animalName}. Your reference number is {app.ReferenceNumber}.\n\n" +
        "A volunteer will review your application, usually within 2 business days. You can track its progress under My Applications on PawConnect.\n\n" +
        "Hope & Paws Animal Shelter",
        app.Email, app.Phone, app.AdopterId);

    public static NotificationRequest ApplicationApproved(AdoptionApplication app, string animalName) => new(
        $"Good news - your application for {animalName} is approved",
        $"Hi {FirstName(app.FullName)},\n\nYour application {app.ReferenceNumber} to adopt {animalName} has been approved. " +
        "We'll be in touch to arrange a home visit and the adoption fee before you take them home.\n\nHope & Paws Animal Shelter",
        app.Email, app.Phone, app.AdopterId);

    public static NotificationRequest ApplicationRejected(AdoptionApplication app, string animalName) => new(
        $"Update on your application for {animalName}",
        $"Hi {FirstName(app.FullName)},\n\nThank you for your interest in {animalName}. Unfortunately we can't approve application {app.ReferenceNumber} at this time." +
        (string.IsNullOrWhiteSpace(app.ReviewNotes) ? "" : $"\n\nNote from our team: {app.ReviewNotes}") +
        "\n\nPlease do browse our other animals. Many are still looking for a home.\n\nHope & Paws Animal Shelter",
        app.Email, app.Phone, app.AdopterId);

    public static NotificationRequest AdoptionCompleted(AdoptionApplication app, string animalName) => new(
        $"Welcome home, {animalName}!",
        $"Hi {FirstName(app.FullName)},\n\nThe adoption of {animalName} is complete. Thank you for giving them a home!\n\nHope & Paws Animal Shelter",
        app.Email, app.Phone, app.AdopterId);

    public static NotificationRequest ShiftConfirmed(ApplicationUser volunteer, Shift shift) => new(
        $"Shift confirmed: {shift.Role} on {ShelterTime.ToLocal(shift.StartsAt):ddd d MMM HH:mm}",
        $"Hi {volunteer.FirstName},\n\nYou're signed up for {shift.Role} on {ShelterTime.ToLocal(shift.StartsAt):dddd d MMMM} " +
        $"from {ShelterTime.ToLocal(shift.StartsAt):HH:mm} to {ShelterTime.ToLocal(shift.EndsAt):HH:mm}.\n\nThank you!\nHope & Paws Animal Shelter",
        volunteer.Email, volunteer.PhoneNumber, volunteer.Id,
        Enums.NotificationUrgency.High);

    public static NotificationRequest DonationReceipt(Receipts.DonationReceipt receipt, string? email, Guid donorId) => new(
        $"Your donation receipt {receipt.ReceiptNumber}",
        receipt.ToPlainText(),
        email, null, donorId);

    private static string FirstName(string fullName) =>
        string.IsNullOrWhiteSpace(fullName) ? "there" : fullName.Trim().Split(' ')[0];
}
