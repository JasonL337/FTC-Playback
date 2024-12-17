using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Compatibility;
using UnityEngine;

public class FollowPath : MonoBehaviour
{
    // HACK
    double curAngle = 0;
    //HACK
    double prevAngle = 0;
    // HACK
    double lastAngleDiff = 0;
    double progress = 0;
    public Transform arrow;

    // This is the previous position of the robot the frame before (distance along the path)
    double[] prevIntersect;

    // This is effectively where you are on the path (including negative position if you haven't got to the starting point)
    public double curPos = 0;
    public double prevPos = 0;

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

    // Just the marker for the simulation. 
    public Transform markerTransform;
    // This is the variable used for the simulation which the only reason I am instantiating this/creating an object
    // is because I need to get the robot poisition which I will do with the localizer. This part on the robot will
    // be a reference to the localizer.
    public CreatePath createPath;

    // This is the number of inches the robot is from the trajectory before the power to the tangent and perpendicular is .5 each.
    public static double TRAJ_WEIGHT_CONST = 5;

    // All for speed control.
    // This is the number of inches the robot is from the end of the trajectory before it starts to slow down.
    double robotSpeed = 0;
    public static double ENDPT_CONST = 20;
    public static double PCONST = .5;
    public static double DCONST = .1;
    double prevError = 0;
    bool activeFollower = false;
    int trajectoryNumber = 1;


    
    // Start is called before the first frame update
    void Start()
{        
        // Just parses through the file and sets each waypoint's (starting at wp0) data to the various arrays
        String[] lines = File.ReadAllLines(@"C:\src\ftc\Venom2024-2025IntoTheDeep\Paths\PathTest.txt");
        activeFollower = true;
        int index = 0;
        dydxs = new double[lines.Length];
        dists = new double[lines.Length];
        totalDists = new double[lines.Length];
        thetas = new double[lines.Length];
        wayPointPoss = new Vector2[lines.Length];
        curPos = 0;
        prevPos = 0;
        prevError = 0;
        combinedDist = 0;
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
            startPos = Mathf.Max(firstString.IndexOf(":") + 2, firstString.IndexOf("-"));
            int commaPos = firstString.IndexOf(",") + 1;
            double posX = 0;
            posX = double.Parse(firstString.Substring(startPos, commaPos - startPos - 1));

            curString = curString.Remove(0, commaPos + 1);

            removePos =  curString.IndexOf(";");
            double posY = double.Parse(curString.Substring(0, removePos - 1));

            wayPointPoss[index] = new Vector2((float)posX, (float)posY);
            index++;
        }
        prevIntersect = new double[]{wayPointPoss[0].x, wayPointPoss[0].y};
        curAngle = getWeightedAngle();  
    }

    // This increments the current trajectory it is on, called by the moverobot class (or the robot master in android studio)
    public void incrementTrajNumber()
    {
        trajectoryNumber++;
    }

    // Based on the current position of the robot, get the number of waypoints that are between the robot and the max
    // look ahead point.
    private int getNumWPsLookahead()
    {
        calcProgress();
        combinedDist = 0;
        int numWPs = 0;
        if (curPos > totalDists[totalDists.Length - 1])
        {
            combinedDist = curPos - totalDists[totalDists.Length - 1];
            return 0;
        }
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

    private double[] getIntersectOfTajectory(){
        // Creating the localized field position. This is only for the simulation. There will be no "convert to"
        // in the actual robot. This line will just be something like "getPosition".
        Vector2 fieldPos = createPath.ConvertToInchesField(createPath.ConvertToNormalizedField(this.transform.position, true));
        // Using the theta, this is the dydx of the cur line/traj.
        double slope = getSlopeOfGrossTraj();
        // Math to find the x and y intersection of the trajectory.
        double[] intersect = new double[2];
        intersect[0] = (fieldPos.y - wayPointPoss[curWP].y + fieldPos.x / slope + slope * wayPointPoss[curWP].x) / (1/slope + slope);
        intersect[1] = wayPointPoss[curWP].y + slope * (intersect[0] - wayPointPoss[curWP].x);
        return intersect;
    }

    private double getSlopeOfGrossTraj()
    {
        double tan = Mathf.Tan((float)(curAngle * Mathf.Deg2Rad));
        if (double.IsInfinity(tan) || double.IsNaN(tan) || double.IsNegativeInfinity(tan))
            tan = 100;
        int sign = Math.Sign(Mathf.Sign(Mathf.Cos((float)curAngle * Mathf.Deg2Rad)) * Mathf.Sign(Mathf.Sin((float)curAngle * Mathf.Deg2Rad)));
        if (sign == 0)
            sign = 1;
        double slope = 1 / Math.Min(Math.Max(Math.Abs(tan), .01f), 100) * sign;
        return slope;
    }

    private double getWeightedAngle(){
        // Theta is the variable that is added on to each iteration and will be the final weighted angle.
        // DistRemaining is the ever changing distance the waypoints are from the robot, once DistRemaining exceeds
        // The look ahead max distance (combinedDist), it caps out.
        double theta = 0;
        double distRemaining = 0;

        int lookAheadWPs = getNumWPsLookahead();



        if (curPos > totalDists[totalDists.Length - 1] || activeFollower == false)
        {
            activeFollower = false;
            Vector2 fieldPos = createPath.ConvertToInchesField(createPath.ConvertToNormalizedField(this.transform.position, true));
            combinedDist = getGeneralDist(wayPointPoss[wayPointPoss.Length - 1].x - fieldPos.x, fieldPos.y - wayPointPoss[wayPointPoss.Length - 1].y);
            double diffy = fieldPos.y - wayPointPoss[wayPointPoss.Length - 1].y;
            double diffx = fieldPos.x - wayPointPoss[wayPointPoss.Length - 1].x;
            theta = Math.Atan2(-diffx, -diffy) * Mathf.Rad2Deg;
            if (Input.GetKey(KeyCode.L))
            {
                if (theta > 88 && theta < 92)
                {
                    Debug.Log(Math.Atan2(diffx, diffy) * Mathf.Rad2Deg);
                    Debug.Log("theta " + theta);
                    Debug.Log("diffx " + diffx);
                    Debug.Log("diffy " + diffy);
                }
            }
            return theta;
        }
        else
        {
            // Loops through every waypoint that is within the look ahead distance
            for (int i = curWP; i < curWP + lookAheadWPs; i++)
            {
                if (Math.Abs(curAngle) > 180)
                    Debug.Log("curang " + curAngle);
                
                // Length of line is literally the length of the line from the robot (if 1st iteration)
                // or this waypoint to the next waypoint.
                double lengthOfLine = totalDists[i + 1] - totalDists[i];

                // WP1 and 2 are the distances from the robot to the current waypoint begin and end markers.
                // to the current waypoint begin and end markers.
                double wp1 = totalDists[i] - curPos;
                double wp2 = totalDists[i + 1] - curPos;
                double deltaTheta = doGimbleCalc(curAngle, thetas[i]);

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
                theta += lengthOfLine * deltaTheta * (((combinedDist - wp1) + (combinedDist - wp2)) / 2.0);
            }
                            
                theta = theta / (Math.Pow(combinedDist, 2) / 2.0);
                if (theta + curAngle > 180)
                    return theta + curAngle - 360;
                else if (theta + curAngle < -180)
                    return theta + curAngle + 360;
                else
                    return theta + curAngle;
        }
    }

    // Simple gimble calculation (returns difference in angles) where clockwise change is positive.
    private double doGimbleCalc(double prevAngle, double curAngle)
    {
        if (curAngle - prevAngle > 180)
            return curAngle - prevAngle - 360;
        if (curAngle - prevAngle < -180)
            return curAngle - prevAngle + 360;
        return curAngle - prevAngle;
    }


    public double speedController()
    {
        if (curPos >= totalDists[totalDists.Length - 1] - ENDPT_CONST || activeFollower == false)
        {
            Vector2 fieldPos = createPath.ConvertToInchesField(createPath.ConvertToNormalizedField(this.transform.position, true));
            double PIDVal;
            double multiplier = Math.Abs(combinedDist / ENDPT_CONST * PCONST);
            double error = getGeneralDist(wayPointPoss[wayPointPoss.Length - 1].x - fieldPos.x, wayPointPoss[wayPointPoss.Length - 1].y - fieldPos.y);
            double d = 0;
            if (prevError != 0)
            {
                d = (error - prevError) / Time.deltaTime * DCONST;
            }
            PIDVal = multiplier * error + d * Math.Sign(error);
            prevError = error;
            robotSpeed = PIDVal;
        }
        else
        {
            robotSpeed = 1;
        }
        return robotSpeed;
        // My robot gets to a point which is within 2 inches or over the max distance and it starts to slow down based solely off of the trajectory
        // from it and the final position and PID control. How do I do that.
    }

    // Calculates the current waypoint it is at depending on the curPos which is the perpendicular line interseciton with the current
    // trajectory.
    private void calcProgress() {
        progress = curPos / totalDists[totalDists.Length - 1];
        if (prevPos <= curPos)
        {
            for (int wp = curWP; wp < totalDists.Length; wp++) {
                if (Input.GetKey(KeyCode.P))
                {
                    Debug.Log("curwp " + curWP);
                    Debug.Log("WP " + wp);
                }
                if (totalDists[wp] > curPos)
                {
                    curWP = Math.Max(wp - 1, 0);
                    break;
                }
            }
        }
        else
        {
            for (int wp = curWP; wp > -1; wp--) {
                if (Input.GetKey(KeyCode.P))
                {
                    Debug.Log("curwp " + curWP);
                    Debug.Log("WP " + wp);
                }
                if (totalDists[wp] < curPos)
                {
                    curWP = wp;
                    break;
                }
            }
        }
    }

    private double getDistToWP(double[] intersect)
    {
        return Math.Sqrt(Math.Pow(intersect[0] - wayPointPoss[curWP].x, 2) + Math.Pow(intersect[1] - wayPointPoss[curWP].y, 2));
    }

    private void updatePos()
    {
        float cos = Mathf.Cos((float)curAngle * Mathf.Deg2Rad);
        double[] curIntersect = getIntersectOfTajectory();
        if (prevIntersect != null)
        {
            prevPos = curPos;
            if (cos == 0)
            {
                if (curAngle > 0)
                    curPos += Math.Sqrt(Math.Pow(curIntersect[0] - prevIntersect[0], 2) + Math.Pow(curIntersect[1] - prevIntersect[1], 2)) * Mathf.Sign((float)curIntersect[0] - (float)prevIntersect[0]);
                else
                    curPos += Math.Sqrt(Math.Pow(curIntersect[0] - prevIntersect[0], 2) + Math.Pow(curIntersect[1] - prevIntersect[1], 2)) * -Mathf.Sign((float)curIntersect[0] - (float)prevIntersect[0]);
            }
            curPos += Math.Sqrt(Math.Pow(curIntersect[0] - prevIntersect[0], 2) + Math.Pow(curIntersect[1] - prevIntersect[1], 2)) * Mathf.Sign(cos) * Mathf.Sign((float)curIntersect[1] - (float)prevIntersect[1]);
        }
        else
            prevIntersect = new double[2];
        // Constantly setting the current angle of the trajector and alligning the arrow accordingly.
        curAngle = getWeightedAngle();
        try{
            if (curAngle != double.NaN)
                arrow.transform.localEulerAngles = new Vector3(0, 0, 90f - (float)curAngle);
        }
        catch{
            Debug.Log("curAngle " + curAngle);
        }
        prevIntersect = getIntersectOfTajectory();

    }

    private double getGeneralDist(double val1, double val2)
    {
        return Math.Sqrt(Math.Pow(val1, 2) + Math.Pow(val2, 2));
    }

    // Gets the normalized vector where x is the normalized x distance, y is the y, and z is the speed.
    public double[] getRobotTrajectory()
    {
        if (activeFollower)
        {
            Vector2 fieldPos = createPath.ConvertToInchesField(createPath.ConvertToNormalizedField(this.transform.position, true));
            double slope = getSlopeOfGrossTraj();
            
            // Just the normalized direction it is supposed to go.
            double lengthOfVector = getGeneralDist(slope, 1);
            int xSign = Math.Sign(curAngle);
            int ySign = Math.Sign(Math.Cos(curAngle * Mathf.Deg2Rad));
            double[] trajPar = new double[]{1.0 / lengthOfVector * xSign, Mathf.Abs((float)slope) / lengthOfVector * ySign};

            // The normalized distance in the perp direction.
            double[] intersect = getIntersectOfTajectory();
            double[] trajPerp = new double[]{intersect[0] - fieldPos.x, intersect[1] - fieldPos.y};
            double lengthOfPerpVector = getGeneralDist(trajPerp[0], trajPerp[1]);
            trajPerp[0] /= lengthOfPerpVector;
            trajPerp[1] /= lengthOfPerpVector;


            // gets the distance between the robot and its intersect.
            double dist = lengthOfPerpVector;

            double powerPerp = dist / (dist + TRAJ_WEIGHT_CONST);
            double powerPar = 1 - powerPerp;

            double[] finalTraj = new double[]{powerPerp * trajPerp[0] + powerPar * trajPar[0], powerPar * trajPar[1] + powerPerp * trajPerp[1]};
            double lengthOfFinalVector = getGeneralDist(finalTraj[0], finalTraj[1]);
            finalTraj[0] /= lengthOfFinalVector;
            finalTraj[1] /= lengthOfFinalVector;
            if (Input.GetKey(KeyCode.S))
            {
                Debug.Log("final traj 0 " + finalTraj[0] + " final traj 1 " + finalTraj[1]);
            }
            return finalTraj;
        }
        else
        {
            double sin = Math.Sin((90 + curAngle) * Mathf.Deg2Rad);
            double cos = Math.Cos((90 + curAngle) * Mathf.Deg2Rad);
            double length = Math.Abs(sin) + Math.Abs(cos);
            double[] finalTraj = new double[]{-cos / length, sin / length};
            return finalTraj;
        }
    }

    public bool isAtEnd()
    {
        return Math.Abs(curPos - totalDists[totalDists.Length - 1]) <= 1;
    }

    // Update is called once per frame 
    void Update()
    {
        // Hack for quickly and consistently increasing the position of the robot
        if (Input.GetKey(KeyCode.F))
            curPos += Time.deltaTime * 8;


        // Hack for printing the intersection and the distance to the line.
        if (Input.GetKey(KeyCode.D))
        {
            double[] intersect = getIntersectOfTajectory();
            Debug.Log("intersect x " + intersect[0] + " y " + intersect[1]);
            Vector3 markerWorldPos = createPath.ConvertToWorldPos(createPath.ConvertFromInchesField(new Vector2((float)intersect[0], (float)intersect[1])));
            Debug.Log("pos x " + markerWorldPos.x + " y " + markerWorldPos.y);
            markerTransform.transform.position = new Vector3(markerWorldPos.x, markerWorldPos.y, 0f);
        }

        // Reread the file.
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Start();
        }

        if (Input.GetKey(KeyCode.T))
        {
            double[] traj = getRobotTrajectory();
            Debug.Log(traj[0] + " is x and y is " + traj[1]);
        }


        
        updatePos();


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
