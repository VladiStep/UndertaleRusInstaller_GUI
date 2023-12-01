using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using Avalonia.Platform;
using System;
using System.Collections.Generic;

namespace UndertaleRusInstallerGUI
{
    public class PlanksControl : Control
    {
        private readonly Random random = new();
        private double prevRes = -1;
        private readonly double yOffset = 3;
        private readonly ImmutableSolidColorBrush mainColor = new(Color.FromRgb(166, 74, 0));
        private readonly ImmutableSolidColorBrush borderColor = new(Color.FromRgb(127, 38, 0));
        private readonly Dictionary<double, double> vertGaps = new(); // <yMult, x>
        private readonly Bitmap dogBMP = new(AssetLoader.Open(new Uri("avares://UndertaleRusInstallerGUI/Assets/finish_dog.png")));

        public override void Render(DrawingContext context)
        {
            context.DrawRectangle(mainColor, null, new Rect(0, 0, Bounds.Width, 3));

            int max = (int)(Bounds.Height / 15 + 1);
            if (max > vertGaps.Count)
            {
                // Add new gaps
                for (int yMult = vertGaps.Count; yMult < max; yMult++)
                {
                    double res;
                    if (yMult == 0)
                    {
                        res = random.Next(1, 16);
                    }
                    else if (yMult % 2 == 1)
                    {
                        res = 0;
                    }
                    else
                    {
                        res = random.Next(1, 16);
                        while (res == prevRes || (res < 7 == prevRes < 7))
                            res = random.Next(1, 16);

                        prevRes = res;
                    }

                    vertGaps[yMult] = res * 10;
                }
            }

            foreach (var gap in vertGaps)
            {
                context.DrawRectangle(borderColor, null, new Rect(0, gap.Key * 15 + yOffset, Bounds.Width, 3));
                context.DrawRectangle(mainColor, null, new Rect(0, 3 + gap.Key * 15 + yOffset, Bounds.Width, 12));
                if (gap.Value != 0)
                    context.DrawRectangle(borderColor, null, new Rect(gap.Value, 3 + gap.Key * 15 + yOffset, 3, 12));
            }

            if (max > 7)
                context.DrawImage(dogBMP, new Rect(10, 10 * 10, dogBMP.Size.Width, dogBMP.Size.Height));

            base.Render(context);
        }
    }
}
