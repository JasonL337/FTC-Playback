using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Compatibility;
using UnityEngine;

public class FollowPath : MonoBehaviour
{
    double prevTime = 0;
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
    List<List<double>> dydxs;

    // distance from last way point to this one (wp 0 is 0)
    List<List<double>> dists;

    // total distance up TO that way point. (wp 0 is 0)
    List<List<double>> totalDists;
    List<List<Vector2>> wayPointPoss;
    List<List<double>> thetas;


    // These are all the lists of the current trajectory it is on.
    List<double> curDydxs;
    List<double> curDists;
    List<double> curTotalDists;
    List<Vector2> curWayPointPoss;
    List<double> curThetas;

    // The current trajectory number it is on.
    int TrajNumber = 0;

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
    int trajectoryNumber = 0;


    
    // Start is called before the first frame update
    void Start()
{        
        // Just parses through the file and sets each waypoint's (starting at wp0) data to the various arrays
        String[] lines = File.ReadAllLines(@"C:\src\ftc\Venom2024-2025IntoTheDeep\Paths\PathTest.txt");
        activeFollower = true;
        int index = 0;
        dydxs = new List<List<double>>();
        dists = new List<List<double>>();
        totalDists = new List<List<double>>();
        wayPointPoss = new List<List<Vector2>>();
        thetas = new List<List<double>>();

        dydxs.Add(new List<double>());
        dists.Add(new List<double>());
        totalDists.Add(new List<double>());
        wayPointPoss.Add(new List<Vector2>());
        thetas.Add(new List<double>());

        curWP = 0;
        curPos = 0;
        prevPos = 0;
        prevError = 0;
        combinedDist = 0;
        // This is the local variable that is the current trajectory of the path that it is dealing with in the file.
        int curTraj = 0;
        foreach (String s in lines)
        {
            String curString = s;
            // If it reaches a BREAK in the file, it creates the current trajectory to increase and to add a new list of points to the lists.
            if (curString.Equals("BREAK"))
            {
                if (index != lines.Length - 1)
                {
                    dydxs.Add(new List<double>());
                    dists.Add(new List<double>());
                    totalDists.Add(new List<double>());
                    wayPointPoss.Add(new List<Vector2>());
                    thetas.Add(new List<double>());
                    curTraj++;
                }
                index++;
                continue;
            }
            int removePos = curString.IndexOf(";");
            int startPos = curString.IndexOf("dYdX:");
            dydxs[curTraj].Add(double.Parse(curString.Substring(startPos + 5, removePos - startPos - 5)));
            curString = curString.Remove(0, removePos + 1);

            removePos =  curString.IndexOf(";");
            startPos = curString.IndexOf(":");
            totalDists[curTraj].Add(double.Parse(curString.Substring(startPos + 1, removePos - startPos - 1)));
            curString = curString.Remove(0, removePos + 1);

            removePos =  curString.IndexOf(";");
            startPos = curString.IndexOf(":");
            dists[curTraj].Add(double.Parse(curString.Substring(startPos + 1, removePos - startPos - 1)));
            curString = curString.Remove(0, removePos + 1);

            removePos =  curString.IndexOf(";");
            startPos = curString.IndexOf(":");
            thetas[curTraj].Add(double.Parse(curString.Substring(startPos + 1, removePos - startPos - 1)));
            curString = curString.Remove(0, removePos + 1);

            String firstString = curString.Substring(0, curString.IndexOf(",") + 1);
            startPos = Mathf.Max(firstString.IndexOf(":") + 2, firstString.IndexOf("-"));
            int commaPos = firstString.IndexOf(",") + 1;
            double posX = 0;
            posX = double.Parse(firstString.Substring(startPos, commaPos - startPos - 1));

            curString = curString.Remove(0, commaPos + 1);

            removePos =  curString.IndexOf(";");
            double posY = double.Parse(curString.Substring(0, removePos - 1));

            wayPointPoss[curTraj].Add(new Vector2((float)posX, (float)posY));
            index++;
        } 

        // Setting the current trajectory data to these lists here.
        curDydxs = dydxs[trajectoryNumber];
        curDists = dists[trajectoryNumber];
        curTotalDists = totalDists[trajectoryNumber];
        curWayPointPoss = wayPointPoss[trajectoryNumber];
        curThetas = thetas[trajectoryNumber];

        prevIntersect = new double[]{wayPointPoss[0][0].x, wayPointPoss[0][0].y};
        curAngle = getWeightedAngle(); 
    }

    // This is the number of trajectories in the path.
    public int getTotalTrajectories()
    {
        return dydxs.Count;
    }

    // This increments the current trajectory it is on, called by the moverobot class (or the robot master in android studio)
    public void incrementTrajNumber()
    {
        // Setting the current trajectory data to these lists here and incrementing the trajectory number.
        trajectoryNumber++;
        curDydxs = dydxs[trajectoryNumber];
        curDists = dists[trajectoryNumber];
        curTotalDists = totalDists[trajectoryNumber];
        curWayPointPoss = wayPointPoss[trajectoryNumber];
        curThetas = thetas[trajectoryNumber];
        activeFollower = true;
        curWP = 0;
        curPos = 0;
        prevPos = 0;
        prevError = 0;
        combinedDist = 0;
        prevIntersect = new double[]{wayPointPoss[trajectoryNumber][0].x, wayPointPoss[trajectoryNumber][0].y};
        lookAheadDist = 10;
        combinedDist = 0;
        curAngle = getWeightedAngle(); 
    }

    // Based on the current position of the robot, get the number of waypoints that are between the robot and the max
    // look ahead point.
    private int getNumWPsLookahead()
    {
        calcProgress();
        combinedDist = 0;
        int numWPs = 0;
        if (curPos > curTotalDists[curTotalDists.Count - 1])
        {
            combinedDist = curPos - curTotalDists[curTotalDists.Count - 1];
            return 0;
        }
        for (int i = curWP; i < curTotalDists.Count - 1; i++)
        {
            if (i == curWP)
            {
                combinedDist += curDists[i + 1] + curTotalDists[i] - curPos;
            }
            else
            {
                combinedDist += curDists[i + 1];
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
        if (trajectoryNumber != 0)
        {
           // Debug.Log("greater than 0");
        }
        double[] intersect = new double[2];
        if (curWP > curWayPointPoss.Count - 1)
        {
          //  Debug.Log("ERROR");
        }
        intersect[0] = (fieldPos.y - curWayPointPoss[curWP].y + fieldPos.x / slope + slope * curWayPointPoss[curWP].x) / (1/slope + slope);
        intersect[1] = curWayPointPoss[curWP].y + slope * (intersect[0] - curWayPointPoss[curWP].x);
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



        if (curPos > curTotalDists[curTotalDists.Count - 1] || activeFollower == false)
        {
            activeFollower = false;
            Vector2 fieldPos = createPath.ConvertToInchesField(createPath.ConvertToNormalizedField(this.transform.position, true));
            combinedDist = getGeneralDist(curWayPointPoss[curWayPointPoss.Count - 1].x - fieldPos.x, fieldPos.y - curWayPointPoss[curWayPointPoss.Count - 1].y);
            double diffy = fieldPos.y - curWayPointPoss[curWayPointPoss.Count - 1].y;
            double diffx = fieldPos.x - curWayPointPoss[curWayPointPoss.Count - 1].x;
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
                double lengthOfLine = curTotalDists[i + 1] - curTotalDists[i];

                // WP1 and 2 are the distances from the robot to the current waypoint begin and end markers.
                // to the current waypoint begin and end markers.
                double wp1 = curTotalDists[i] - curPos;
                double wp2 = curTotalDists[i + 1] - curPos;
                double deltaTheta = doGimbleCalc(curAngle, curThetas[i]);

                // If it's the first waypoint, the first waypoint distance is 0.
                if (i == curWP)
                {
                    wp1 = 0;
                    lengthOfLine = curTotalDists[i + 1] - curPos;
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
        if (curPos >= curTotalDists[curTotalDists.Count - 1] - ENDPT_CONST || activeFollower == false)
        {
            Vector2 fieldPos = createPath.ConvertToInchesField(createPath.ConvertToNormalizedField(this.transform.position, true));
            double PIDVal;
            double multiplier = Math.Abs(combinedDist / ENDPT_CONST * PCONST);
            double error = getGeneralDist(curWayPointPoss[curWayPointPoss.Count - 1].x - fieldPos.x, curWayPointPoss[curWayPointPoss.Count - 1].y - fieldPos.y);
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
            double weightedSum = 0;
            double remainingLookAheadDist = lookAheadDist;

            for (int i = curWP; i < curWayPointPoss.Count - 1; i++)
            {
                Vector2 wp = curWayPointPoss[i];
                Vector2 nextWp = curWayPointPoss[i + 1];

                double angleDiff = Math.Abs(doGimbleCalc(curThetas[i], curAngle));
                double segmentDist = curDists[i];

                if (i == curWP)
                {
                    // Truncate the first segment distance
                    segmentDist = curTotalDists[i + 1] - curPos;
                }

                if (remainingLookAheadDist < segmentDist)
                {
                    // Truncate the last segment distance
                    segmentDist = remainingLookAheadDist;
                }

                weightedSum += angleDiff * segmentDist;
                remainingLookAheadDist -= segmentDist;

                if (remainingLookAheadDist <= 0)
                {
                    break;
                }
            }
            if (remainingLookAheadDist <= 0)
                weightedSum /= lookAheadDist;
            else
                weightedSum /= lookAheadDist - remainingLookAheadDist;
            
            Debug.Log("weighted sum " + weightedSum);
            robotSpeed = Math.Pow(Math.E, -weightedSum / 60);
        }
        return robotSpeed;
        // My robot gets to a point which is within 2 inches or over the max distance and it starts to slow down based solely off of the trajectory
        // from it and the final position and PID control. How do I do that.
    }

    // Calculates the current waypoint it is at depending on the curPos which is the perpendicular line interseciton with the current
    // trajectory.
    private void calcProgress() {
        progress = curPos / curTotalDists[curTotalDists.Count - 1];
        if (prevPos <= curPos)
        {
            for (int wp = curWP; wp < curTotalDists.Count; wp++) {
                if (curTotalDists[wp] > curPos)
                {
                    curWP = Math.Max(wp - 1, 0);
                    break;
                }
            }
        }
        else
        {
            for (int wp = curWP; wp > -1; wp--) {
                if (curTotalDists[wp] < curPos)
                {
                    curWP = wp;
                    break;
                }
            }
        }
    }

    private double getDistToWP(double[] intersect)
    {
        return Math.Sqrt(Math.Pow(intersect[0] - curWayPointPoss[curWP].x, 2) + Math.Pow(intersect[1] - curWayPointPoss[curWP].y, 2));
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
        if (Input.GetKeyDown(KeyCode.P))
        {
            Debug.Log("In");
        }
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
        Vector2 fieldPos = createPath.ConvertToInchesField(createPath.ConvertToNormalizedField(this.transform.position, true));
        return getGeneralDist(fieldPos.x - curWayPointPoss[curWayPointPoss.Count - 1].x, fieldPos.y - curWayPointPoss[curWayPointPoss.Count - 1].y) < 2 && !activeFollower;//Math.Abs(curPos - curTotalDists[curTotalDists.Count - 1]) <= 1;
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

        if (Input.GetKey(KeyCode.T) && false)
        {
            double[] traj = getRobotTrajectory();
            Debug.Log(traj[0] + " is x and y is " + traj[1]);
        }


        if (Time.time > prevTime + 1/30)
        {
            updatePos();
            prevTime = Time.time;
        }
        


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
