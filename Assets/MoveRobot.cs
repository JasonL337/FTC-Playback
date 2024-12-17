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

    public double localSpeed = 30;

    public FollowPath followPath;

    public CreatePath createPath;
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
        if (Input.GetKey(KeyCode.G))
            followPath.getRobotTrajectory();
        if ((Time.time > .5 || !followPath.isAtEnd()) && Input.GetKey(KeyCode.L))
        {
            setTargetVel();
            velocityX += Math.Sign(tarVelX - velocityX) * Time.deltaTime * localSpeed;
            velocityY += Math.Sign(tarVelY - velocityY) * Time.deltaTime * localSpeed;
            //Debug.Log("tarvel x " + tarVelX + "tarvel y " + tarVelY);

            Vector2 worldVel = createPath.ConvertToWorldPos(createPath.ConvertFromInchesField(new Vector2((float)velocityX, (float)velocityY)));
            this.transform.localPosition = new Vector2(this.transform.localPosition.x + worldVel.x * Time.deltaTime, this.transform.localPosition.y + worldVel.y * Time.deltaTime);
        }
        
    }
}
