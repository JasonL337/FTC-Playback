using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveRobot : MonoBehaviour
{

    double velocityX = 0;
    double velocityY = 0;
    double tarVelX = 0;
    double tarVelY = 0;

    double prevTime = 0;

    public double localSpeed = 60;

    public FollowPath followPath;

    public CreatePath createPath;

    float mechanismTime = 3;
    float mechanismTimer = 0;

    String state = "traj 1";
    int curTraj = 1;
    int totalTrajs = 0;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    void setTargetVel()
    {
        double[] tar = followPath.getRobotTrajectory();
        double speed = followPath.speedController();
        tarVelX = tar[0] * localSpeed * speed;
        tarVelY = tar[1] * localSpeed * speed;
    }

    // Update is called once per frame
    void Update()
    {
        // At the beginning, set the total number of trajectories the robot knows about by using the follow path script.
        if (Time.time == 0)
            totalTrajs = followPath.getTotalTrajectories();
        if (Time.time > prevTime + 1/30)
        {
            prevTime = Time.time;
            
            // Debg to get the robot trajectory
            if (Input.GetKey(KeyCode.G))
                followPath.getRobotTrajectory();

            // Boolean for whether the robot has finished its current trajectory
            bool finishedTraj = followPath.isAtEnd();

            // If the robot is not at the end of the trajectory, and the user is pressing the L key, and the robot is not in the mechanism state, move the robot.
            if ((Time.time > .5 || !finishedTraj) && Input.GetKey(KeyCode.L) && !state.Equals("mechanism") && !state.Equals("finished"))
            {
                setTargetVel();
                velocityX += Math.Sign(tarVelX - velocityX) * Time.deltaTime * localSpeed;
                velocityY += Math.Sign(tarVelY - velocityY) * Time.deltaTime * localSpeed;
                //Debug.Log("tarvel x " + tarVelX + "tarvel y " + tarVelY);

                Vector2 worldVel = createPath.ConvertToWorldPos(createPath.ConvertFromInchesField(new Vector2((float)velocityX, (float)velocityY)));
                this.transform.localPosition = new Vector2(this.transform.localPosition.x + worldVel.x * Time.deltaTime, this.transform.localPosition.y + worldVel.y * Time.deltaTime);
            }
            // If it's in the "mechanism" state, pause until the mechanism timer exceeds the max mechanism time (default 3 seconds).
            else if (state.Equals("mechanism"))
            {
                if (mechanismTimer > mechanismTime)
                {
                    mechanismTimer = 0;
                    state = "traj " + curTraj;
                }
                else
                {
                    mechanismTimer += Time.deltaTime;
                }
            }

            if (finishedTraj)
            {
                if (curTraj == totalTrajs)
                {
                    state = "finished";
                    Debug.Log("finished");
                }
                else
                {
                    curTraj++;
                    state = "mechanism";
                    Debug.Log("mechanism");
                    followPath.incrementTrajNumber();
                    finishedTraj = false;
                }
            }
        }
        
    }
}
