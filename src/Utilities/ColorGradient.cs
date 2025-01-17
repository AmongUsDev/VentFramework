using System.Linq;
using System.Text;
using UnityEngine;
using VentLib.Logging;

namespace VentLib.Utilities;

public class ColorGradient
{
    private Color[] colors;
    private float spacing;

    public ColorGradient(params Color[] colors)
    {
        this.colors = colors;
        spacing = 1f / (this.colors.Length - 1);
    }

    public Color Evaluate(float percent)
    {
        if (percent > 1) percent = 1;
        int indexLow = Mathf.FloorToInt(percent / spacing);
        if (indexLow >= colors.Length - 1) return colors[^1];
        int indexHigh = indexLow + 1;
        float percentClamp = (colors.Length - 1) * (percent - indexLow * spacing);

        Color colorA = colors[indexLow];
        Color colorB = colors[indexHigh];

        float r = colorA.r + percentClamp * (colorB.r - colorA.r);
        float g = colorA.g + percentClamp * (colorB.g - colorA.g);
        float b = colorA.b + percentClamp * (colorB.b - colorA.b);

        return new Color(r, g, b);
    }

    public string Apply(string input)
    {
        if (input.Length == 0) return input;
        if (input.Length == 1) return colors[0].Colorize(input);
        float step = 1f / (input.Length - 1);
        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            builder.Append(Evaluate(step * i).Colorize(c.ToString()));
        }

        return builder.ToString();
    }
}