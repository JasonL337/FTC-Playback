using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class FollowPath : MonoBehaviour
{
    double progress = 0;
    public Transform arrow;
    public double curPos = 0;

    // This is the last way point you are AT OR PASSED (starts at 0)
    int curWP = 0;
    public double lookAheadDist = 10;
    private double combinedDist = 0;
    double[] dydxs;

    // distance from last way point to this one (wp 0 is 0)
    double[] dists;

    // total distance up TO that way point. (wp 0 is 0)
    double[] totalDists;
    Vector2[] wayPointPoss;
    double[] thetas;

    
    // Start is called before the first frame update
    void Start()
    {
        String[] lines = File.ReadAllLines(@"C:\src\ftc\Venom2024-2025IntoTheDeep\Paths\PathTest.txt");
        int index = 0;
        dydxs = new double[lines.Length];
        dists = new double[lines.Length];
        totalDists = new double[lines.Length];
        thetas = new double[lines.Length];
        wayPointPoss = new Vector2[lines.Length];
        foreach (String s in lines)
        {
            String curString = s;
            int removePos = curString.IndexOf(";");
            int startPos = curString.IndexOf("dYdX:");
            dydxs[index] = double.Parse(curString.Substring(startPos + 5, removePos - startPos - 5));
            curString = curString.Remove(0, removePos + 1);

            removePos =  curString.IndexOf(";");
            startPos = curString.IndexOf(":");
            totalDists[index] = double.Parse(curString.Substring(startPos + 1, removePos - startPos - 1));
            curString = curString.Remove(0, removePos + 1);

            removePos =  curString.IndexOf(";");
            startPos = curString.IndexOf(":");
            dists[index] = double.Parse(curString.Substring(startPos + 1, removePos - startPos - 1));
            curString = curString.Remove(0, removePos + 1);

            removePos =  curString.IndexOf(";");
            startPos = curString.IndexOf(":");
            thetas[index] = double.Parse(curString.Substring(startPos + 1, removePos - startPos - 1));
            curString = curString.Remove(0, removePos + 1);

            String firstString = curString.Substring(0, curString.IndexOf(",") + 1);
            Debug.Log(firstString);
            startPos = Mathf.Max(firstString.IndexOf(":") + 2, firstString.IndexOf("-") + 1);
            Debug.Log(startPos);
            int commaPos = firstString.IndexOf(",");
            // HACK
            double posX = 0;//double.Parse(firstString.Substring(startPos + 2, commaPos - startPos - 1));
            curString = curString.Remove(0, commaPos + 1);

            commaPos = Mathf.Max(curString.IndexOf(",") + 2, curString.IndexOf("-") + 1);
            removePos =  curString.IndexOf(";");
            startPos = Mathf.Max(curString.IndexOf(",") + 2, curString.IndexOf("-") + 1);
            //HACK
            double posY = 0;//double.Parse(curString.Substring(2, removePos - commaPos - 2));

            wayPointPoss[index] = new Vector2((float)posX, (float)posY);
            index++;
        }
    }

    // Based on the current position of the robot, get the number of waypoints that are between the robot and the max
    // look ahead point.
    private int getNumWPsLookahead()
    {
        calcProgress();
        combinedDist = 0;
        int numWPs = 0;
        for (int i = curWP; i < totalDists.Length - 1; i++)
        {
            if (i == curWP)
            {
                combinedDist += dists[i + 1] + totalDists[i] - curPos;
            }
            else
            {
                combinedDist += dists[i + 1];
            }
            if (combinedDist > lookAheadDist)
            {
                combinedDist = lookAheadDist;
                numWPs++;
                break;
            }
            numWPs++;
        }
        return numWPs;
    }

    private double getWeightedAngle(){
        // Theta is the variable that is added on to each iteration and will be the final weighted angle.
        // DistRemaining is the ever changing distance the waypoints are from the robot, once DistRemaining exceeds
        // The look ahead max distance (combinedDist), it caps out.
        double theta = 0;
        double distRemaining = 0;

        // Loops through every waypoint that is within the look ahead distance
        for (int i = curWP; i < curWP + getNumWPsLookahead(); i++)
        {
            
            // Length of line is literally the length of the line from the robot (if 1st iteration)
            // or this waypoint to the next waypoint.
            double lengthOfLine = totalDists[i + 1] - totalDists[i];

            // WP1 and 2 are the distances from the robot to the current waypoint begin and end markers.
            // to the current waypoint begin and end markers.
            double wp1 = totalDists[i] - curPos;
            double wp2 = totalDists[i + 1] - curPos;

            // If it's the first waypoint, the first waypoint distance is 0.
            if (i == curWP)
            {
                wp1 = 0;
                lengthOfLine = totalDists[i + 1] - curPos;
            }

            // Capping the length of the line if it is greater than the look ahead distance.
            if (distRemaining + lengthOfLine > combinedDist)
            {
                lengthOfLine = combinedDist - distRemaining;
                distRemaining = combinedDist;
                wp2 = combinedDist;
            }
            else
            {
                // If the line isn't too big/exceeds look ahead, it adds the length of the current line 
                // between waypoints to the distance from the robot.
                distRemaining += lengthOfLine;
            }

            // Doing the area of the trapezoid that encompases this area of the angle.
            theta += lengthOfLine * thetas[i] * (((distRemaining - wp1) + (distRemaining - wp2)) / 2.0);
        }
                    
        theta = theta / (Math.Pow(combinedDist, 2) / 2.0);
        return theta;
    }

    private void calcProgress() {
        progress = curPos / totalDists[totalDists.Length - 1];
        for (int wp = curWP; wp < totalDists.Length - curWP; wp++) {
            if (Input.GetKey(KeyCode.P))
            {
                Debug.Log("curwp " + curWP);
                Debug.Log("WP " + wp);
            }
            if (totalDists[wp] > curPos)
            {
                curWP = wp - 1;
                
                break;
            }
        }
    }

    // Update is called once per frame 
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
            Start();
        arrow.transform.localEulerAngles = new Vector3(0, 0, 90f - (float)getWeightedAngle());
        //string path = @"C:\src\ftc\Venom2024-2025IntoTheDeep\Paths\PathTest.txt";
        /*
        Requirements:
        Start the robot and localize its position
        Move along the trajectory based off weighted perpendicular and weighted parallel trajectories
        Determine weighted trajectories based off cur progress
        Determine speed based off of dot product of all the weighted trajs?


        Math for weighted derivative given an array of all the derivatives and their progress to the whole:
        based off of distance to my cur position up to 10 inches?
        so at 10 inches, it doesn't take into account anymore?

        Create const accel. Don't just set power determining velocity

        Different math areas:
        Using localization to determine cur velocity with relation to power and then accel as a result. Using calculated desired speed

        Find weighted traj for desired speed and direction

        Find progress of robot along path

        Localization


        var maxrange = 10 inches
        integral of the dydx * however long it is / max range = weight it gets


            On inititialization, create an array locally of all the points
            current progress 
            every frame, find weighted average based on progress
            progress is determined by

        */

        //float slope = -1f / 
    }
}
