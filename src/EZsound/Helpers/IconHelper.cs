using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace EZsound.Helpers;

/// <summary>
/// Helper to extract application icons from executable paths.
/// </summary>
public static class IconHelper
{
    private static readonly Dictionary<string, ImageSource?> _iconCache = new(StringComparer.OrdinalIgnoreCase);
    private static ImageSource? _defaultIcon;

    /// <summary>
    /// Gets the icon for an application by its executable path.
    /// Returns a default icon if the executable path is unavailable or icon extraction fails.
    /// </summary>
    public static ImageSource? GetIconForProcess(string? executablePath, uint processId)
    {
        // Try executable path first
        if (!string.IsNullOrEmpty(executablePath))
        {
            if (_iconCache.TryGetValue(executablePath, out var cached))
                return cached ?? GetDefaultIcon();

            var icon = ExtractIconFromFile(executablePath);
            _iconCache[executablePath] = icon;
            return icon ?? GetDefaultIcon();
        }

        // Try to get the executable path from the process ID
        if (processId != 0)
        {
            var path = ProcessHelper.GetExecutablePath(processId);
            if (!string.IsNullOrEmpty(path))
            {
                if (_iconCache.TryGetValue(path, out var cached))
                    return cached ?? GetDefaultIcon();

                var icon = ExtractIconFromFile(path);
                _iconCache[path] = icon;
                return icon ?? GetDefaultIcon();
            }
        }

        return GetDefaultIcon();
    }

    private static ImageSource? ExtractIconFromFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return null;

            using var icon = Icon.ExtractAssociatedIcon(filePath);
            if (icon == null) return null;

            var bitmapSource = Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());

            bitmapSource.Freeze(); // Make it cross-thread accessible
            return bitmapSource;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[EZsound] Failed to extract icon from {filePath}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Returns a default speaker icon created programmatically.
    /// </summary>
    public static ImageSource GetDefaultIcon()
    {
        if (_defaultIcon != null)
            return _defaultIcon;

        try
        {
            // Create a simple default icon using DrawingImage
            var drawing = new GeometryDrawing
            {
                Brush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(160, 160, 180)),
                Geometry = Geometry.Parse(
                    "M 8,4 L 8,20 L 14,15 L 20,18 L 20,6 L 14,9 Z")
            };

            var drawingImage = new DrawingImage(drawing);
            drawingImage.Freeze();
            _defaultIcon = drawingImage;
        }
        catch
        {
            _defaultIcon = null;
        }

        return _defaultIcon!;
    }

    /// <summary>
    /// Clears the icon cache.
    /// </summary>
    public static void ClearCache()
    {
        _iconCache.Clear();
    }
}
