using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using Serilog;

namespace MontiorUserStandlone
{
  public class Screenshot
  {
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
      public int Left;
      public int Top;
      public int Right;
      public int Bottom;
    }

    [DllImport("user32.dll")]
    static extern bool SetProcessDPIAware();

    [DllImport("user32.dll")]
    static extern bool IsProcessDPIAware();

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    public static byte[] CaptureActiveWindow(Process process, bool captureScreenshot)
    {
      IntPtr hWnd = GetForegroundWindow();
      RECT rect;

      StringBuilder windowText = new StringBuilder(256);
      GetWindowText(hWnd, windowText, windowText.Capacity);

      if (process.MainWindowTitle.CompareTo(windowText.ToString()) == 0)
      {
        if (GetWindowRect(hWnd, out rect))
        {
          if (captureScreenshot)
          {
            if (Environment.OSVersion.Version.Major >= 11)
            {
              SetProcessDPIAware();
            }
            Rectangle bounds = Screen.PrimaryScreen.Bounds;

            // Create a bitmap of the size of the primary screen
            using (
              Bitmap screenshot = new Bitmap(
                bounds.Width,
                bounds.Height,
                PixelFormat.Format32bppArgb
              )
            )
            {
              using (Graphics g = Graphics.FromImage(screenshot))
              {
                // Copy the screen contents to the bitmap, including the taskbar
                g.CopyFromScreen(
                  bounds.Location,
                  Point.Empty,
                  bounds.Size,
                  CopyPixelOperation.SourceCopy
                );
              }

              using (MemoryStream ms = new MemoryStream())
              {
                // Set JPEG encoder
                ImageCodecInfo jpgEncoder = ImageCodecInfo
                  .GetImageDecoders()
                  .First(c => c.FormatID == ImageFormat.Jpeg.Guid);
                EncoderParameters encoderParams = new EncoderParameters(1);
                encoderParams.Param[0] = new EncoderParameter(
                  System.Drawing.Imaging.Encoder.Quality,
                  20L
                ); // 60% quality for medium compression

                screenshot.Save(ms, jpgEncoder, encoderParams);

                // // Check size
                byte[] screenshotBytes = ms.ToArray();

                if (screenshotBytes.Length > 400 * 1024)
                {
                  Log.Warning("Screenshot exceeds 400 KB limit. Consider lowering quality.");
                  // Optionally reduce quality more here or resize image
                }

                return screenshotBytes;
              }
            }
          }
          else
          {
            return Encoding.UTF8.GetBytes("");
          }
        }
      }

      return null;
    }
  }
}
