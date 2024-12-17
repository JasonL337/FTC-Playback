using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class CreatePath : MonoBehaviour
{
    // Create me a list of LineRenderers
    private List<LineRenderer> lineRenderers;
    // The last position of the mouse
    private Vector2 oldPos;
    bool firstFrame = true;
    public Transform cameraPos;
    public Transform cameraRot;
    public Transform fieldPos;

    public List<WayPoint> wayPoints = new List<WayPoint>();
    float totalDist = 0;
    int curRend = -1;

   // public Vector2[]
    // Start is called before the first frame update
    void Start()
    {
        lineRenderers = new List<LineRenderer>();
        createLineRenderer();
        wayPoints = new List<WayPoint>();
    }





    public Vector2 ConvertToWorldPos(Vector2 normFieldPos) {
        float angleInRad = Mathf.Deg2Rad * fieldPos.transform.localEulerAngles.z;
        float posX = fieldPos.transform.position.x;
        float posY = fieldPos.transform.position.y;
        Vector2 iPrime = new Vector2(fieldPos.transform.localScale.x / 1.235f * 1.13f * Mathf.Cos(angleInRad), 
                                     fieldPos.transform.localScale.x / 1.235f * 1.13f * Mathf.Sin(angleInRad));
        Vector2 jPrime = new Vector2(fieldPos.transform.localScale.y / 1.235f * 1.13f * Mathf.Cos(angleInRad + 90 * Mathf.Deg2Rad), 
                                     fieldPos.transform.localScale.y / 1.235f * 1.13f * Mathf.Sin(angleInRad + 90 * Mathf.Deg2Rad));

        return new Vector2(normFieldPos.x * iPrime.x + normFieldPos.y * jPrime.x + posX, normFieldPos.x * iPrime.y + normFieldPos.y * jPrime.y + posY);
    }






    public Vector2 ConvertToNormalizedField(Vector2 worldPos, bool isLocalPos)
    {
        // Creating a centralized worldPos to be used in case there is a translation in the fieldPos so no matter
        // where it is, it's always in relation to the center of the field.
        Vector2 centralizedWorldPos = new Vector2(worldPos.x - fieldPos.transform.position.x, worldPos.y - fieldPos.transform.position.y);

        // scaleX and scaleY are the shurnken or expanded i hat and j hat without any other transformations. 
        // Could be added as one full transformation with a shrink/expand plus rotation but it's prettier this way.
        float scaleX = 1/(fieldPos.transform.localScale.x * 1.13f);
        float scaleY = 1/(fieldPos.transform.localScale.y * 1.13f);
        if (isLocalPos)
        {
            scaleX *= 1.235f;
            scaleY *= 1.235f;
        }
        float angleInRad = Mathf.Deg2Rad * fieldPos.transform.localEulerAngles.z;

        // iPrime and jPrime transformations by rotation and scaling and then applying to the centralizedWorldPos.
        Vector2 iPrime = new Vector2(scaleX * Mathf.Cos(angleInRad), scaleX * -Mathf.Sin(angleInRad));
        Vector2 jPrime = new Vector2(scaleY * Mathf.Sin(angleInRad), scaleY * Mathf.Cos(angleInRad));

        return new Vector2(centralizedWorldPos.x * iPrime.x + centralizedWorldPos.y * jPrime.x, centralizedWorldPos.x * iPrime.y + centralizedWorldPos.y * jPrime.y);
    }

    public Vector2 ConvertFromInchesField(Vector2 inchFieldPos)
    {
        // 72 inches per side of the map, so converting normalized field position to inches.
        Vector2 iPrime = new Vector2(1/72.0f, 0);
        Vector2 jPrime = new Vector2(0, 1/72.0f);

        return new Vector2(inchFieldPos.x * iPrime.x + inchFieldPos.y * jPrime.x, inchFieldPos.x * iPrime.y + inchFieldPos.y * jPrime.y);
    }





    public Vector2 ConvertToInchesField(Vector2 normFieldPos)
    {
        // 77 inches per side of the map, so converting normalized field position to inches.
        Vector2 iPrime = new Vector2(72, 0);
        Vector2 jPrime = new Vector2(0, 72);

        return new Vector2(normFieldPos.x * iPrime.x + normFieldPos.y * jPrime.x, normFieldPos.x * iPrime.y + normFieldPos.y * jPrime.y);
    }




    private void ReallignWayPoints()
    {

        // Loop through each point in the LineRenderer
        for (int i = 0; i < lineRenderers[curRend].positionCount; i++)
        {
            // Creating the newPos of each waypoint using ConvertToWorldPos.
            Vector3 normFieldPos = wayPoints[i].normFieldPos;
            Vector3 newPos = ConvertToWorldPos(normFieldPos);//new Vector3(normFieldPos.x * iPrime.x + normFieldPos.y * jPrime.x + fieldPos.transform.position.x, normFieldPos.x * iPrime.y + normFieldPos.y * jPrime.y + fieldPos.transform.position.y, -.0001f);
            
            // Set the position of each point to newPos
            lineRenderers[curRend].SetPosition(i, new Vector3(newPos.x, newPos.y, -.0001f));
        }
    }



    private void setWayPoints(bool firstFrame, Vector3 worldPosition) {
        // If it's the first frame, you simply set the first position in the path/line to be where you click (can't connect
        // any points bc it's the first point) AND you set the first "oldPos" to be where on the field in inches you clicked.
        if (!firstFrame)
        {
            // Converting worldspace to normalized space and inch-"ized" space on the field.
            Vector2 normFieldPos = ConvertToNormalizedField(worldPosition, false);
            Vector2 truePos = ConvertToInchesField(normFieldPos);

            // Finding change in x in inches and change in y in inches from the previous point.
            float deltaX = truePos.x - oldPos.x;
            float deltaY = truePos.y - oldPos.y;
            
            float dist = Mathf.Sqrt(deltaX * deltaX + deltaY * deltaY);
            // If the distance in the path is greater than .5 inches, set a new waypoint.
            if (dist > 1f)
            {
                // Setting the waypoint
                lineRenderers[curRend].positionCount++;
                Vector3 linePos = new Vector3(worldPosition.x, worldPosition.y, fieldPos.position.z - .0001f);
                lineRenderers[curRend].SetPosition(lineRenderers[curRend].positionCount - 1, linePos);

                totalDist += dist;

                // Creating the new waypoiny as an object and adding it to the list of waypoints
                WayPoint newWP = new WayPoint(worldPosition, normFieldPos, truePos, totalDist, dist);
                wayPoints.Add(newWP);

                // Checking if it's not the first waypoint, then set the previos one's "next" to the new one.
                if (lineRenderers[curRend].positionCount != 1)
                {
                    wayPoints[lineRenderers[curRend].positionCount - 2].setNext(newWP);
                }

                oldPos = truePos;
            }
        }
        else
        {
            // Setting the first waypoint and setting the first "oldPos" to the first waypoint in terms of inches.
            lineRenderers[curRend].SetPosition(0, new Vector3(worldPosition.x, worldPosition.y, fieldPos.position.z - .0001f));
            Vector3 normFieldPos = ConvertToNormalizedField(worldPosition, false);
            oldPos = ConvertToInchesField(normFieldPos);

            // Creating the new waypoint as an object and adding it to the list of waypoints
            WayPoint newWP = new WayPoint(new Vector3(worldPosition.x, worldPosition.y, -.0001f), normFieldPos, oldPos, 0, 0);
            wayPoints.Add(newWP);
        }
    }


    private void compileWayPoints()
    {
        string path = @"C:\src\ftc\Venom2024-2025IntoTheDeep\Paths\PathTest.txt";
        //UnityEngine.Windows.Directory.roamingFolder;
        String[] vals = new string[wayPoints.Count];
        for (int i = 0; i < vals.Length; i++) {
            vals[i] = "pos:" + i + ", " + wayPoints[i].toString();
        }
        System.IO.File.WriteAllLines(path, vals);
    }

    private void createLineRenderer()
    {
        curRend++;
        GameObject newGameObject = new GameObject("Path " + (curRend + 1));
        LineRenderer newLineRenderer = newGameObject.AddComponent<LineRenderer>();

        // Setting the line renderer pretty/visual details.
        if (newLineRenderer != null)
        {
            newLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            newLineRenderer.widthMultiplier = .03f;
            newLineRenderer.positionCount = 1;
            int mod = curRend % 3;
            newLineRenderer.startColor = new Color(mod * .5f, mod * .5f, mod * .5f);
            Gradient gradient = new Gradient();
            GradientColorKey[] colorKey = new GradientColorKey[2];
            GradientAlphaKey[] alphaKey = new GradientAlphaKey[2];

            // Set the color keys at the relative time 0 and 1 (0 is the start, 1 is the end)
            colorKey[0].color = new Color(mod * .5f, mod * .5f, mod * .5f);
            colorKey[0].time = 0.0f;
            colorKey[1].color = new Color(mod * .5f, mod * .5f, mod * .5f);
            colorKey[1].time = 1.0f;

            // Set the alpha keys at the relative time 0 and 1
            alphaKey[0].alpha = 1.0f;
            alphaKey[0].time = 0.0f;
            alphaKey[1].alpha = 0.0f;
            alphaKey[1].time = 1.0f;

            gradient.SetKeys(colorKey, alphaKey);

            newLineRenderer.colorGradient = gradient;
            newLineRenderer.endColor = new Color(mod * .5f, mod * .5f, mod * .5f);
            lineRenderers.Add(newLineRenderer);
        }
    }

    // Update is called once per frame
    void Update()
    {
        // Occurs every frame the mouse is clicked
        if (Input.GetMouseButton(0)){

            // Converting screen to world positon using Unity.
            Vector3 screenPosition = new Vector3(Input.mousePosition.x, Input.mousePosition.y, fieldPos.position.z - cameraPos.position.z);
            Vector3 worldPosition = Camera.main.ScreenToWorldPoint(screenPosition);
            
            // If it's the first frame, you simply set the first position in the path/line to be where you click (can't connect
            // any points bc it's the first point) AND you set the first "oldPos" to be where on the field in inches you clicked.
            setWayPoints(firstFrame, worldPosition);

           // Debug.Log(worldPosition);

            firstFrame = false;
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            ReallignWayPoints();
        }

        if (Input.GetKeyDown(KeyCode.B))
        {
            createLineRenderer();
        }

        if (Input.GetKeyDown(KeyCode.C))
        {
            compileWayPoints();
        }
    }
}
