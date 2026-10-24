using System.Globalization;
using Domain.Shared;

namespace Application.Mail;

/// <summary>Every mail Culina sends, in English and German.</summary>
/// <remarks>Plain words, the same voice as the app; a link always says how long it works.</remarks>
internal static class MailWording
{
    // Spelt out here: the server runs with invariant globalization, so it has no German month names to borrow.
    private static readonly string[] EnglishMonths =
        ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"];

    private static readonly string[] GermanMonths =
        ["Januar", "Februar", "März", "April", "Mai", "Juni", "Juli", "August", "September", "Oktober", "November", "Dezember"];

    internal static MailWords ConfirmAddress(Language language, string name, Uri link) => language switch
    {
        Language.De => new(
            "Bestätige deine E-Mail-Adresse für Culina",
            $"Hallo {name},",
            ["bitte bestätige, dass diese Adresse dir gehört. Erst danach kannst du dich anmelden. Der Link gilt 48 Stunden."],
            "Adresse bestätigen",
            link,
            "Du hast kein Culina-Konto angelegt? Dann ignoriere diese Mail einfach."),
        _ => new(
            "Confirm your email address for Culina",
            $"Hello {name},",
            ["please confirm that this address is yours. You can sign in once you have. The link works for 48 hours."],
            "Confirm address",
            link,
            "Did not create a Culina account? Then just ignore this mail.")
    };

    internal static MailWords SignInLink(Language language, string name, Uri link) => language switch
    {
        Language.De => new(
            "Dein Anmeldelink für Culina",
            $"Hallo {name},",
            ["mit diesem Link meldest du dich ohne Passwort an. Er funktioniert einmal und 15 Minuten lang."],
            "Anmelden",
            link,
            "Du hast keinen Link angefordert? Dann ignoriere diese Mail. Ohne den Link kann sich niemand anmelden."),
        _ => new(
            "Your Culina sign-in link",
            $"Hello {name},",
            ["this link signs you in without a password. It works once, for 15 minutes."],
            "Sign in",
            link,
            "Did not ask for a link? Then ignore this mail. Nobody can sign in without it.")
    };

    internal static MailWords PasswordReset(Language language, string name, Uri link, string code) => language switch
    {
        Language.De => new(
            "Neues Passwort für Culina",
            $"Hallo {name},",
            [
                "über diesen Link wählst du ein neues Passwort. Er funktioniert einmal und eine Stunde lang.",
                $"Falls sich der Link nicht öffnen lässt, gib auf der Seite „Passwort vergessen“ diesen Code ein: {code}"
            ],
            "Neues Passwort wählen",
            link,
            "Du hast das nicht angefordert? Dann ignoriere diese Mail, dein Passwort bleibt wie es ist."),
        _ => new(
            "Choose a new Culina password",
            $"Hello {name},",
            [
                "this link lets you choose a new password. It works once, for one hour.",
                $"If the link does not open, enter this code on the forgotten-password page: {code}"
            ],
            "Choose a new password",
            link,
            "Did not ask for this? Then ignore this mail; your password stays as it is.")
    };

    internal static MailWords NewDevice(Language language, string name, string device, DateTimeOffset at, Uri link) =>
        language switch
        {
            Language.De => new(
                "Neue Anmeldung bei Culina",
                $"Hallo {name},",
                [
                    $"am {When(language, at)} hat sich jemand mit {device} in deinem Konto angemeldet.",
                    "Warst du das, ist alles in Ordnung. Wenn nicht, melde das Gerät ab und ändere dein Passwort."
                ],
                "Geräte ansehen",
                link,
                "Culina schreibt dir, wenn sich ein Gerät anmeldet, das es von dir noch nicht kennt."),
            _ => new(
                "New sign-in to your Culina account",
                $"Hello {name},",
                [
                    $"somebody signed in to your account from {device} on {When(language, at)}.",
                    "If that was you, there is nothing to do. If it was not, sign that device out and change your password."
                ],
                "Review your devices",
                link,
                "Culina tells you when a device it has not seen you use before signs in.")
        };

    internal static MailWords PasswordChanged(Language language, string name, DateTimeOffset at, Uri link) => language switch
    {
        Language.De => new(
            "Dein Culina-Passwort wurde geändert",
            $"Hallo {name},",
            [
                $"dein Passwort wurde am {When(language, at)} geändert, und alle anderen Geräte wurden abgemeldet.",
                "Warst du das nicht, setze dein Passwort jetzt zurück."
            ],
            "Passwort zurücksetzen",
            link,
            "Culina schreibt dir bei jeder Passwortänderung."),
        _ => new(
            "Your Culina password was changed",
            $"Hello {name},",
            [
                $"your password was changed on {When(language, at)}, and every other device was signed out.",
                "If that was not you, reset your password now."
            ],
            "Reset your password",
            link,
            "Culina tells you whenever your password changes.")
    };

    internal static MailWords RecoveryCodesMade(Language language, string name, DateTimeOffset at, Uri link) => language switch
    {
        Language.De => new(
            "Neue Wiederherstellungscodes für dein Culina-Konto",
            $"Hallo {name},",
            [
                $"am {When(language, at)} wurden neue Wiederherstellungscodes erstellt; die alten gelten nicht mehr.",
                "Warst du das nicht, ändere sofort dein Passwort."
            ],
            "Sicherheit öffnen",
            link,
            "Culina schreibt dir, wenn sich etwas daran ändert, wie man in dein Konto kommt."),
        _ => new(
            "New recovery codes for your Culina account",
            $"Hello {name},",
            [
                $"a new set of recovery codes was made on {When(language, at)}; the old ones no longer work.",
                "If that was not you, change your password straight away."
            ],
            "Open security settings",
            link,
            "Culina tells you when anything changes about how to get into your account.")
    };

    internal static MailWords RecoveryCodeIssued(Language language, string name, DateTimeOffset at, Uri link) => language switch
    {
        Language.De => new(
            "Ein Wiederherstellungscode für dein Culina-Konto",
            $"Hallo {name},",
            [
                $"die Person, die dieses Culina verwaltet, hat am {When(language, at)} einen einmaligen Wiederherstellungscode für dein Konto erstellt. Er gilt 24 Stunden.",
                "Hast du nicht darum gebeten, sprich sie an."
            ],
            "Sicherheit öffnen",
            link,
            "Culina schreibt dir, wenn sich etwas daran ändert, wie man in dein Konto kommt."),
        _ => new(
            "A recovery code was issued for your Culina account",
            $"Hello {name},",
            [
                $"the person who runs this Culina issued a one-time recovery code for your account on {When(language, at)}. It works for 24 hours.",
                "If you did not ask them for one, talk to them."
            ],
            "Open security settings",
            link,
            "Culina tells you when anything changes about how to get into your account.")
    };

    internal static MailWords Invitation(
        Language language,
        string inviter,
        string household,
        DateTimeOffset expiresAt,
        Uri link) => language switch
        {
            Language.De => new(
                $"{inviter} lädt dich zu „{household}“ bei Culina ein",
                "Hallo,",
                [$"{inviter} möchte Rezepte, Wochenplan und Einkaufsliste in „{household}“ mit dir teilen. Die Einladung gilt bis {Day(language, expiresAt)}."],
                "Einladung annehmen",
                link,
                "Du kennst die Person nicht? Dann ignoriere diese Mail einfach."),
            _ => new(
                $"{inviter} invited you to “{household}” on Culina",
                "Hello,",
                [$"{inviter} would like to share recipes, the meal plan and the shopping list in “{household}” with you. The invitation works until {Day(language, expiresAt)}."],
                "Accept the invitation",
                link,
                "Do not know who this is? Then just ignore this mail.")
        };

    internal static MailWords Test(Language language, string name) => language switch
    {
        Language.De => new(
            "Testmail von Culina",
            $"Hallo {name},",
            ["diese Mail kommt von Culina. Der Mailversand ist richtig eingerichtet."],
            null,
            null,
            "Du hast diese Mail in den Maileinstellungen angefordert."),
        _ => new(
            "Test mail from Culina",
            $"Hello {name},",
            ["this mail comes from Culina. Sending mail is set up correctly."],
            null,
            null,
            "You asked for this mail in the mail settings.")
    };

    private static string When(Language language, DateTimeOffset at)
    {
        var time = at.UtcDateTime.ToString("HH:mm", CultureInfo.InvariantCulture);

        return language == Language.De ? $"{Day(language, at)} um {time} UTC" : $"{Day(language, at)} at {time} UTC";
    }

    private static string Day(Language language, DateTimeOffset at)
    {
        var day = at.UtcDateTime;

        return language == Language.De
            ? $"{day.Day}. {GermanMonths[day.Month - 1]} {day.Year}"
            : $"{day.Day} {EnglishMonths[day.Month - 1]} {day.Year}";
    }
}
