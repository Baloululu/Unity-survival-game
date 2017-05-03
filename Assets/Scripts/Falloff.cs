using System.Collections;
using UnityEngine;

public static class Falloff {

	public static float[,] GenerateFallOff (int size)
    {
        float[,] fallMap = new float[size, size];

        for (int i = 0; i < size; ++i)
        {
            for (int j = 0; j < size; ++j)
            {
                float xValue = i / (float) size * 2 - 1;
                float yValue = j / (float) size * 2 - 1;

                float value = Mathf.Max(Mathf.Abs(xValue), Mathf.Abs(yValue));
                fallMap[i, j] = Evaluate(value);
            }
        }

        return fallMap;
    }

    static float Evaluate(float value)
    {
        float a = 3;
        float b = 2.2f;

        return Mathf.Pow(value, a) / (Mathf.Pow(value, a) + Mathf.Pow(b - b * value, a));
    }
}
