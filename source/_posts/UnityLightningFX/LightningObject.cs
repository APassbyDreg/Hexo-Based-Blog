using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LightningObject : MonoBehaviour
{
    [SerializeField]
    private bool animate = true;

    [SerializeField]
    private int seed; // random seed

    [SerializeField]
    private Vector3 startPos = new Vector3(-5, 0, 0); // where lightning starts

    [SerializeField]
    private Vector3 endPos = new Vector3(5, 0, 0); // where lightning ends

    [SerializeField]
    private float startWidth = 0.02f; // with at the start

    [SerializeField]
    private float endWidth = 0.02f; // with at the end

    [SerializeField]
    private int numKeyPoints = 20;

    [SerializeField]
    [Range(0.0f, 1.0f)]
    private float evolutionStart = 0.0f;

    [SerializeField]
    [Range(0.0f, 1.0f)]
    private float evolutionEnd = 1.0f;


    [SerializeField]
    [Range(0.0f, 1.0f)]
    private float offsetPercentage = 0.2f;

    [SerializeField]
    private float tangentRandomRadius = 0.5f;


    [SerializeField]
    private AnimationCurve tangentRandomScale = new AnimationCurve(new Keyframe(0, 1), new Keyframe(1, 1));

    [SerializeField]
    private float widthRandomSize = 0.1f;

    [SerializeField]
    private float animationDuration = 1.0f;

    [SerializeField]
    private float arcHeight = 0.5f;

    [SerializeField]
    private float sineHeight = 0.2f;

    [SerializeField]
    private float sineOffset = 0.2f;

    [SerializeField]
    private float noiseEvolutionSpeed = 10.0f;


    private LineRenderer renderer;

    private int rdn;

    private float dist, pos_step, width_step; // 距离和间距
    private Vector3 n, x, y; // 局部坐标系
    private float currAnimationIdx = -1;
    private float currArcMaxHeight, currArcTangentDeg;
    private Vector2 currArcTangentDir;
    private float currSineRotation, currSineLT, currSineRT, currSineHeight, currSinePhi, currSineOffset;

    private void initParams()
    {
        rdn = seed;

        // 计算方向和距离
        Vector3 dir = endPos - startPos;
        dist = dir.magnitude;
        pos_step = dist / (numKeyPoints + 1);
        width_step = (endWidth - startWidth) / (numKeyPoints + 1);

        // 创建局部坐标系
        n = dir.normalized;
        Vector3 tmp = new Vector3(0.0f, 0.0f, 0.0f);
        if (dir.z == 1.0f)
        {
            tmp.x = 1.0f;
        }
        else
        {
            tmp.z = dir.z > 0.0f ? 1.0f : -1.0f;
        }
        x = Vector3.Cross(tmp, n);
        y = Vector3.Cross(n, x);
    }

    void Start()
    {
        renderer = GetComponent<LineRenderer>();
        initParams();
    }

    void Update()
    {
        initParams();
        if (animate)
        {
            Random.InitState(rdn + Time.frameCount);
            rdn = (int)((1 << 32) * Random.value);
        }
        else
        {
            Random.InitState(rdn);
        }

        // timing
        float currTime = animate ? Time.fixedTime : 0;
        float animationIdx = Mathf.Floor(currTime / animationDuration);
        float animationTime = currTime / animationDuration - animationIdx;

        // new animation
        if (currAnimationIdx != animationIdx)
        {
            currAnimationIdx = animationIdx;
            // arc
            currArcMaxHeight = Random.value * arcHeight;
            currArcTangentDeg = Random.value * 2.0f * Mathf.PI;
            currArcTangentDir = new Vector2(Mathf.Sin(currArcTangentDeg), Mathf.Cos(currArcTangentDeg));
            // sine
            currSineLT = Random.value * 0.5f - 0.25f;
            if (currSineLT >= 0)
            {
                currSineLT = Mathf.Max(0.1f, currSineLT);
            }
            currSineRT = Random.value * 0.5f + 0.75f;
            if (currSineRT <= 1)
            {
                currSineRT = Mathf.Min(0.9f, currSineRT);
            }
            currSineHeight = Random.value * sineHeight;
            currSinePhi = Random.value * 2.0f * Mathf.PI;
            currSineOffset = (Random.value * 2.0f - 1.0f) * sineOffset + // offset
                             2.0f * Mathf.Cos(currSinePhi) / Mathf.PI * currSineHeight; // normalize
            currSineRotation = Random.value * 2.0f * Mathf.PI;
        }
        float arcAmp = currArcMaxHeight * animationTime;


        // 生成随机数
        Vector2[] tangentOffsets = new Vector2[numKeyPoints];
        float[] directionalOffsets = new float[numKeyPoints];
        float[] widthOffsets = new float[numKeyPoints];
        for (int i = 0; i < numKeyPoints; i++)
        {
            float r = Mathf.Clamp01(Mathf.PerlinNoise(i * 4 + 0.1f, currTime * noiseEvolutionSpeed + seed)) * 0.75f + 0.25f;
            float phi = Mathf.Clamp01(Mathf.PerlinNoise(i * 4 + 0.9f, currTime * noiseEvolutionSpeed + seed)) * 2.0f * Mathf.PI;
            tangentOffsets[i].x = r * Mathf.Sin(phi);
            tangentOffsets[i].y = r * Mathf.Cos(phi);
            directionalOffsets[i] = Mathf.Clamp01(Mathf.PerlinNoise(i * 4 + 2.2f, currTime * noiseEvolutionSpeed + seed)) - 0.5f;
            widthOffsets[i] = Mathf.Clamp01(Mathf.PerlinNoise(i * 4 + 2.9f, currTime * noiseEvolutionSpeed + seed)) - 0.5f;
        }

        // 计算关键点的位置和粗细
        Vector3[] full_positions = new Vector3[numKeyPoints + 2];
        AnimationCurve full_curve = new AnimationCurve();
        // add first point
        full_curve.AddKey(0.0f, startWidth);
        full_positions[0] = startPos;
        // add mid points
        for (int i = 0; i < numKeyPoints; i++)
        {
            float t = (1 + i + directionalOffsets[i] * offsetPercentage) / (numKeyPoints + 1);
            // pos
            Vector3 pos = startPos + n * pos_step * (i + 1);
            pos += n * pos_step * directionalOffsets[i] * offsetPercentage;
            // arc
            float arcAmpAtT = arcAmp * (1.0f - 4.0f * (t - 0.5f) * (t - 0.5f));
            pos += currArcTangentDir.x * x * arcAmpAtT * tangentRandomScale.Evaluate(t);
            pos += currArcTangentDir.y * y * arcAmpAtT * tangentRandomScale.Evaluate(t);
            // sine
            float sineAmpAtT = 0;
            float phi = currSinePhi + animationTime;
            float sineRotationDeg = (t - 0.5f) * currSineRotation + currArcTangentDeg;
            if (t < currSineLT)
            {
                sineAmpAtT = t / currSineLT * (Mathf.Sin(phi) * currSineHeight + currSineOffset);
            }
            if (t >= currSineLT && t <= currSineRT)
            {
                sineAmpAtT = Mathf.Sin(phi + (t - currSineLT) / (currSineRT - currSineLT) * Mathf.PI) * currSineHeight + currSineOffset;
            }
            if (t > currSineRT)
            {
                sineAmpAtT = (1.0f - t) / (1.0f - currSineRT) * (-Mathf.Sin(phi) * currSineHeight + currSineOffset);
            }
            pos += sineAmpAtT * Mathf.Sin(sineRotationDeg) * x * tangentRandomScale.Evaluate(t);
            pos += sineAmpAtT * Mathf.Cos(sineRotationDeg) * y * tangentRandomScale.Evaluate(t);
            // randomness
            pos += tangentOffsets[i].x * x * tangentRandomRadius * tangentRandomScale.Evaluate(t);
            pos += tangentOffsets[i].y * y * tangentRandomRadius * tangentRandomScale.Evaluate(t);
            full_positions[i + 1] = pos;
            // width
            float width = startWidth + width_step * (i + 1);
            width += widthOffsets[i] * t * widthRandomSize;
            full_curve.AddKey(t, width);
        }
        // add last point
        full_curve.AddKey(1.0f, endWidth);
        full_positions[numKeyPoints + 1] = endPos;

        // filter keypoints in evolution range
        int num_pos = 0;
        List<Vector3> positioins = new List<Vector3>();
        AnimationCurve curve = new AnimationCurve();
        for (int i = 0; i < numKeyPoints + 2; i++)
        {
            if (full_curve[i].time >= evolutionStart && full_curve[i].time <= evolutionEnd)
            {
                positioins.Add(full_positions[i]);
                curve.AddKey(full_curve[i]);
                num_pos++;
            }
        }

        // set renderer
        renderer.positionCount = num_pos;
        renderer.SetPositions(positioins.ToArray());
        renderer.widthCurve = curve;
    }
}

