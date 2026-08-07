using System.Globalization;
using AcliComunicazioni.Application.Mileage;
using AcliComunicazioni.Web.Models;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace AcliComunicazioni.Web.Services;

public static class MileagePdfGenerator
{
    private static readonly CultureInfo ItalianCulture = new("it-IT");
    private static readonly XColor Navy = XColor.FromArgb(23, 50, 77);
    private static readonly XColor Teal = XColor.FromArgb(23, 107, 122);
    private static readonly XColor Muted = XColor.FromArgb(103, 121, 135);
    private static readonly XColor Border = XColor.FromArgb(220, 230, 234);
    private static readonly XColor Surface = XColor.FromArgb(247, 250, 251);
    private static readonly XColor Warning = XColor.FromArgb(154, 91, 0);
    private static readonly XColor WarningSurface = XColor.FromArgb(255, 249, 239);

    public static byte[] Create(MileageHistoryViewModel model, string logoPath)
    {
        var entries = model.Dashboard.RecentEntries
            .OrderByDescending(entry => entry.TripDate)
            .ThenByDescending(entry => entry.Id)
            .ToArray();
        var period = GetPeriod(model);
        var totalKilometers = entries.Sum(entry => entry.DistanceKilometers);
        var gaps = entries.Sum(entry => entry.GapKilometers);
        var minimumKilometers = entries.Length == 0 ? (int?)null : entries.Min(entry => entry.StartKilometers);
        var maximumKilometers = entries.Length == 0 ? (int?)null : entries.Max(entry => entry.EndKilometers);

        using var document = new PdfDocument();
        document.Info.Title = $"Report percorrenze - {period}";
        document.Info.Author = "AcliComunicazioni";
        document.Info.Subject = "Storico percorrenze del mezzo";

        var regular = new XFont("Arial", 8.2, XFontStyleEx.Regular);
        var small = new XFont("Arial", 7.2, XFontStyleEx.Regular);
        var bold = new XFont("Arial", 8.2, XFontStyleEx.Bold);
        var title = new XFont("Arial", 20, XFontStyleEx.Bold);
        var metric = new XFont("Arial", 13, XFontStyleEx.Bold);

        PdfPage page = null!;
        XGraphics graphics = null!;
        var pageNumber = 0;
        var y = 0d;

        void NewPage(bool includeSummary)
        {
            if (graphics is not null)
            {
                DrawFooter(graphics, page, pageNumber, period, small);
                graphics.Dispose();
            }

            page = document.AddPage();
            page.Size = PdfSharp.PageSize.A4;
            page.Orientation = PdfSharp.PageOrientation.Portrait;
            graphics = XGraphics.FromPdfPage(page);
            pageNumber++;
            y = DrawHeader(graphics, page, period, logoPath, title, regular, bold);

            if (includeSummary)
            {
                y = DrawSummary(
                    graphics,
                    page,
                    y,
                    entries.Length,
                    totalKilometers,
                    minimumKilometers,
                    maximumKilometers,
                    gaps,
                    regular,
                    metric);
            }

            y = DrawTableHeader(graphics, page, y, bold);
        }

        NewPage(includeSummary: true);

        if (entries.Length == 0)
        {
            graphics.DrawString(
                "Nessuna percorrenza nel periodo selezionato.",
                regular,
                new XSolidBrush(Muted),
                new XRect(36, y + 18, page.Width.Point - 72, 32),
                CenterFormat());
        }
        else
        {
            foreach (var entry in entries)
            {
                const double rowHeight = 31;
                if (y + rowHeight > page.Height.Point - 45)
                {
                    NewPage(includeSummary: false);
                }

                DrawEntry(graphics, page, y, rowHeight, entry, regular, small, bold);
                y += rowHeight;
            }
        }

        DrawFooter(graphics, page, pageNumber, period, small);
        graphics.Dispose();

        using var stream = new MemoryStream();
        document.Save(stream, false);
        return stream.ToArray();
    }

    private static double DrawHeader(
        XGraphics graphics,
        PdfPage page,
        string period,
        string logoPath,
        XFont title,
        XFont regular,
        XFont bold)
    {
        const double margin = 36;
        if (File.Exists(logoPath))
        {
            using var logo = XImage.FromFile(logoPath);
            graphics.DrawImage(logo, margin, 27, 38, 44);
        }
        else
        {
            graphics.DrawRoundedRectangle(new XSolidBrush(Teal), margin, 32, 34, 34, 7, 7);
            graphics.DrawString("AC", bold, XBrushes.White, new XRect(margin, 32, 34, 34), CenterFormat());
        }

        graphics.DrawString("AcliComunicazioni", bold, new XSolidBrush(Navy), new XRect(84, 32, 220, 17), LeftFormat());
        graphics.DrawString("GESTIONE TRASFERTE", regular, new XSolidBrush(Teal), new XRect(84, 49, 220, 17), LeftFormat());
        graphics.DrawString("Report percorrenze", title, new XSolidBrush(Navy), new XRect(margin, 78, 360, 30), LeftFormat());
        graphics.DrawString(period, regular, new XSolidBrush(Muted), new XRect(margin, 108, 360, 18), LeftFormat());
        graphics.DrawString($"Estratto il {DateTime.Today:dd/MM/yyyy}", regular, new XSolidBrush(Muted), new XRect(page.Width.Point - 220, 83, 184, 20), RightFormat());
        graphics.DrawLine(new XPen(Teal, 1.5), margin, 132, page.Width.Point - margin, 132);
        return 148;
    }

    private static double DrawSummary(
        XGraphics graphics,
        PdfPage page,
        double y,
        int count,
        int totalKilometers,
        int? minimumKilometers,
        int? maximumKilometers,
        int gaps,
        XFont regular,
        XFont metric)
    {
        const double margin = 36;
        const double gap = 8;
        var width = (page.Width.Point - (margin * 2) - gap) / 2;

        DrawMetric(graphics, margin, y, width, "REGISTRAZIONI", count.ToString(ItalianCulture), regular, metric, false);
        DrawMetric(graphics, margin + width + gap, y, width, "CHILOMETRI PERCORSI", $"{totalKilometers.ToString("N0", ItalianCulture)} km", regular, metric, false);
        y += 58;
        DrawMetric(graphics, margin, y, width, "INTERVALLO CONTACHILOMETRI", $"{FormatKm(minimumKilometers)} – {FormatKm(maximumKilometers)}", regular, metric, false);
        DrawMetric(graphics, margin + width + gap, y, width, "DA VERIFICARE", $"{gaps.ToString("N0", ItalianCulture)} km", regular, metric, gaps > 0);
        return y + 72;
    }

    private static void DrawMetric(
        XGraphics graphics,
        double x,
        double y,
        double width,
        string label,
        string value,
        XFont regular,
        XFont metric,
        bool warning)
    {
        graphics.DrawRoundedRectangle(
            new XPen(warning ? XColor.FromArgb(240, 211, 157) : Border, .8),
            new XSolidBrush(warning ? WarningSurface : Surface),
            x,
            y,
            width,
            50,
            6,
            6);
        graphics.DrawString(label, regular, new XSolidBrush(Muted), new XRect(x + 10, y + 7, width - 20, 14), LeftFormat());
        graphics.DrawString(value, metric, new XSolidBrush(warning ? Warning : Navy), new XRect(x + 10, y + 23, width - 20, 20), LeftFormat());
    }

    private static double DrawTableHeader(XGraphics graphics, PdfPage page, double y, XFont font)
    {
        const double margin = 36;
        const double height = 25;
        graphics.DrawRectangle(new XSolidBrush(Navy), margin, y, page.Width.Point - (margin * 2), height);

        var columns = GetColumns(page);
        DrawCellText(graphics, "DATA", font, XBrushes.White, columns[0], y, height, false);
        DrawCellText(graphics, "TRAGITTO", font, XBrushes.White, columns[1], y, height, false);
        DrawCellText(graphics, "KM INIZ.", font, XBrushes.White, columns[2], y, height, true);
        DrawCellText(graphics, "KM FIN.", font, XBrushes.White, columns[3], y, height, true);
        DrawCellText(graphics, "PERCORSI", font, XBrushes.White, columns[4], y, height, true);
        DrawCellText(graphics, "CONDUCENTE", font, XBrushes.White, columns[5], y, height, false);
        return y + height;
    }

    private static void DrawEntry(
        XGraphics graphics,
        PdfPage page,
        double y,
        double height,
        MileageEntry entry,
        XFont regular,
        XFont small,
        XFont bold)
    {
        const double margin = 36;
        var columns = GetColumns(page);
        graphics.DrawRectangle(new XPen(Border, .6), margin, y, page.Width.Point - (margin * 2), height);

        DrawCellText(graphics, entry.TripDate.ToString("dd/MM/yyyy"), regular, new XSolidBrush(Navy), columns[0], y, height, false);
        DrawCellText(graphics, Shorten(entry.Route, 35), bold, new XSolidBrush(Navy), columns[1], y, 17, false);
        if (!string.IsNullOrWhiteSpace(entry.Description))
        {
            DrawCellText(graphics, Shorten(entry.Description, 45), small, new XSolidBrush(Muted), columns[1], y + 13, 15, false);
        }
        DrawCellText(graphics, entry.StartKilometers.ToString("N0", ItalianCulture), regular, new XSolidBrush(Navy), columns[2], y, height, true);
        DrawCellText(graphics, entry.EndKilometers.ToString("N0", ItalianCulture), regular, new XSolidBrush(Navy), columns[3], y, height, true);
        DrawCellText(graphics, $"{entry.DistanceKilometers.ToString("N0", ItalianCulture)} km", bold, new XSolidBrush(Teal), columns[4], y, height, true);
        DrawCellText(graphics, Shorten(entry.InsertedBy, 22), regular, new XSolidBrush(Navy), columns[5], y, height, false);
    }

    private static XRect[] GetColumns(PdfPage page)
    {
        const double x = 36;
        var widths = new[] { 68d, 166d, 62d, 62d, 67d, page.Width.Point - 72 - 425d };
        var columns = new XRect[widths.Length];
        var currentX = x;
        for (var index = 0; index < widths.Length; index++)
        {
            columns[index] = new XRect(currentX, 0, widths[index], 0);
            currentX += widths[index];
        }
        return columns;
    }

    private static void DrawCellText(
        XGraphics graphics,
        string text,
        XFont font,
        XBrush brush,
        XRect column,
        double y,
        double height,
        bool rightAligned)
    {
        var rectangle = new XRect(column.X + 5, y, Math.Max(0, column.Width - 10), height);
        graphics.DrawString(text, font, brush, rectangle, rightAligned ? RightFormat() : LeftFormat());
    }

    private static void DrawFooter(XGraphics graphics, PdfPage page, int pageNumber, string period, XFont font)
    {
        graphics.DrawLine(new XPen(Border, .7), 36, page.Height.Point - 32, page.Width.Point - 36, page.Height.Point - 32);
        graphics.DrawString($"AcliComunicazioni · {period}", font, new XSolidBrush(Muted), new XRect(36, page.Height.Point - 29, 300, 16), LeftFormat());
        graphics.DrawString($"Pagina {pageNumber}", font, new XSolidBrush(Muted), new XRect(page.Width.Point - 180, page.Height.Point - 29, 144, 16), RightFormat());
    }

    private static string GetPeriod(MileageHistoryViewModel model) =>
        model.IsAllHistory || !model.SelectedYear.HasValue
            ? "Tutto lo storico"
            : model.SelectedMonth.HasValue
                ? $"{ItalianCulture.DateTimeFormat.GetMonthName(model.SelectedMonth.Value)} {model.SelectedYear.Value}"
                : $"Anno {model.SelectedYear.Value}";

    private static string FormatKm(int? value) =>
        value.HasValue ? value.Value.ToString("N0", ItalianCulture) : "—";

    private static string Shorten(string value, int maximumLength) =>
        value.Length <= maximumLength ? value : $"{value[..(maximumLength - 1)]}…";

    private static XStringFormat LeftFormat() => new()
    {
        Alignment = XStringAlignment.Near,
        LineAlignment = XLineAlignment.Center
    };

    private static XStringFormat RightFormat() => new()
    {
        Alignment = XStringAlignment.Far,
        LineAlignment = XLineAlignment.Center
    };

    private static XStringFormat CenterFormat() => new()
    {
        Alignment = XStringAlignment.Center,
        LineAlignment = XLineAlignment.Center
    };
}
