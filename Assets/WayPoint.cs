using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WayPoint
{
    public Vector3 normFieldPos;
    public Vector3 fieldPos;
    public Vector3 worldPos;
    public WayPoint next;
    public float dydxToNext = 0;
    public float yInt = 0;
    public float totalDist = 0;
    public float dist = 0;
    public float thetaToVert = 0;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    public WayPoint(Vector3 worldPosWP, Vector3 normFieldPosWP, Vector3 fieldPosWP, float tD, float d)
    {
        worldPos = worldPosWP;
        normFieldPos = normFieldPosWP;
        fieldPos = fieldPosWP;
        totalDist = tD;
        dist = d;
    }

    public void setNext(WayPoint nextWP) {
        next = nextWP;
        Vector3 nextPos = next.fieldPos;
        float posNeg = Mathf.Sign((nextPos.y - this.fieldPos.y) / (nextPos.x - this.fieldPos.x));
        dydxToNext = Mathf.Min(Mathf.Abs(nextPos.y - this.fieldPos.y) / Mathf.Abs(nextPos.x - this.fieldPos.x), 1000) * posNeg;
        yInt = dydxToNext * (-fieldPos.x) + fieldPos.y;
        if ((dydxToNext == 1000 || dydxToNext == -1000) && nextPos.y < this.fieldPos.y)
        {
            thetaToVert = 180 * Math.Sign(dydxToNext);
        }
        else
        {
            if (Mathf.Sign(nextPos.x - this.fieldPos.x) > 0)
                thetaToVert = Mathf.Rad2Deg * Mathf.Acos(Vector2.Dot(new Vector2(0, 1), new Vector2(1, dydxToNext)) / Mathf.Sqrt(1 + Mathf.Pow(dydxToNext, 2)));
            else
                thetaToVert = -(180 - Mathf.Rad2Deg * Mathf.Acos(Vector2.Dot(new Vector2(0, 1), new Vector2(1, dydxToNext)) / Mathf.Sqrt(1 + Mathf.Pow(dydxToNext, 2))));

        }
        Debug.Log("dydx " + dydxToNext);
        Debug.Log("y Int" + yInt);
    }

    public String toString()
    {
        //return "dYdX:" + dydxToNext + "; yInt:" + yInt + "; fieldPos:(" + fieldPos.x + ", " + fieldPos.y + ", " + fieldPos.z + "); normFieldPos:(" + normFieldPos.x + ", " + normFieldPos.y + ", " + normFieldPos.z + "); worldPos:(" + worldPos.x + ", " + worldPos.y + ", " + worldPos.z + "); totalDist:" + totalDist + "; dist:" + dist + "; theta:" + thetaToVert;
        return "dYdX:" + dydxToNext + "; totalDist:" + totalDist + "; dist:" + dist + "; theta:" + thetaToVert + "; fieldPos:(" + fieldPos.x + ", " + fieldPos.y + ");";
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
