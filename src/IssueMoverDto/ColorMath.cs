using System.Globalization;

namespace Hubbup.Web.Utils
{
    public static class ColorMath
    {
        private const float BrightnessThreshold = 0.5f;

        /// <summary>
        /// Given a hex background color (e.g. 4adc55), calculate whether the foreground color
        /// of the text should be white or black in order to be readable with sufficient contrast.
        /// </summary>
        /// <param name="hexBackColor"></param>
        /// <returns></returns>
        public static string GetHexForeColorForBackColor(string hexBackColor)
        {
            var backColorInt = int.Parse(hexBackColor, NumberStyles.HexNumber);

            var r = ((backColorInt & 0xff0000) >> 16) / 255f;
            var g = ((backColorInt & 0x00ff00) >> 8) / 255f;
            var b = ((backColorInt & 0x0000ff) >> 0) / 255f;

            var luma = 0.299 * r + 0.587 * g + 0.114 * b;

            if (luma > BrightnessThreshold)
            {
                return "000000";
            }
            else
            {
                return "ffffff";
            }
        }
    }
}
