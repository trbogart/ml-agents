using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;
using System.Diagnostics;

public class HunterAgent : Agent
{
    HunterPreySettings m_HunterPreySettings;
    public GameObject area;
    HunterPreyArea m_MyArea;
    bool m_Shoot;
    bool m_Capturing;
    float m_CaptureTime;
    Rigidbody m_AgentRb;
    float m_LaserLength;
    // Speed of agent rotation.
    public float turnSpeed = 300;

    // Speed of agent movement.
    public float moveSpeed = 1.5f;
    public Material normalMaterial;
    public Material badMaterial;
    public Material goodMaterial;
    public Material frozenMaterial;
    public GameObject myLaser;
    public bool contribute;
    public bool useVectorObs;
    public float shootSpeedMultiplier = 0.75f;
    public float existentialPenalty = 0;
    public float indivFoodReward = 1;
    public float groupFoodReward = 1;
    public float shootPreyReward = 0.1f;
    public float shootMissPenalty = -0.01f;
    public float captureDuration = 0.5f;


    EnvironmentParameters m_ResetParams;

    public override void Initialize()
    {
        m_AgentRb = GetComponent<Rigidbody>();
        m_MyArea = area.GetComponent<HunterPreyArea>();
        m_HunterPreySettings = FindAnyObjectByType<HunterPreySettings>();
        m_ResetParams = Academy.Instance.EnvironmentParameters;
        SetResetParameters();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (useVectorObs)
        {
            var localVelocity = transform.InverseTransformDirection(m_AgentRb.linearVelocity);
            sensor.AddObservation(localVelocity.x);
            sensor.AddObservation(localVelocity.z);
            sensor.AddObservation(m_Shoot);
        }
    }

    public Color32 ToColor(int hexVal)
    {
        var r = (byte)((hexVal >> 16) & 0xFF);
        var g = (byte)((hexVal >> 8) & 0xFF);
        var b = (byte)(hexVal & 0xFF);
        return new Color32(r, g, b, 255);
    }

    public void MoveAgent(ActionBuffers actionBuffers)
    {
        m_Shoot = false;

        if (m_Capturing && Time.time > m_CaptureTime + captureDuration)
        {
            FinishCapturing();
        }

        var dirToGo = Vector3.zero;
        var rotateDir = Vector3.zero;

        var continuousActions = actionBuffers.ContinuousActions;
        var discreteActions = actionBuffers.DiscreteActions;

        var forward = Mathf.Clamp(continuousActions[0], -1f, 1f);
        var right = Mathf.Clamp(continuousActions[1], -1f, 1f);
        var rotate = Mathf.Clamp(continuousActions[2], -1f, 1f);

        dirToGo = transform.forward * forward;
        dirToGo += transform.right * right;
        rotateDir = -transform.up * rotate;

        if (discreteActions[0] > 0 && !m_Capturing)
        {
            m_Shoot = true;
            dirToGo *= shootSpeedMultiplier;
            m_AgentRb.linearVelocity *= shootSpeedMultiplier;
        }
        m_AgentRb.AddForce(dirToGo * moveSpeed, ForceMode.VelocityChange);
        transform.Rotate(rotateDir, Time.fixedDeltaTime * turnSpeed);

        if (m_AgentRb.linearVelocity.sqrMagnitude > 25f) // slow it down
        {
            m_AgentRb.linearVelocity *= 0.95f;
        }

        if (m_Shoot)
        {
            var myTransform = transform;
            myLaser.transform.localScale = new Vector3(1f, 1f, m_LaserLength);
            var rayDir = 25.0f * myTransform.forward;
            Debug.DrawRay(myTransform.position, rayDir, Color.red, 0f, true);
            RaycastHit hit;
            if (Physics.SphereCast(transform.position, 2f, rayDir, out hit, 25f))
            {
                PreyAgent prey = hit.collider.gameObject.GetComponent<PreyAgent>();
                if (prey != null)
                {
                    if (prey.Freeze()) 
                    {
                        AddReward(shootPreyReward);
                    }
                    // else already frozen, no reward or penalty
                }
                else
                {
                    AddReward(shootMissPenalty);   
                }
            } 
            else
            {
                AddReward(shootMissPenalty);
            }
        }
        else
        {
            myLaser.transform.localScale = new Vector3(0f, 0f, 0f);
        }
    }

    void Capture()
    {
        Debug.Log("Hunter captured prey!");
        AddReward(indivFoodReward);
        m_MyArea.OnPreyCaptured(groupFoodReward);
        if (contribute)
        {
            m_HunterPreySettings.totalScore += 1;
        }

        m_Capturing = true;
        m_CaptureTime = Time.time;
        ChangeColor();
    }

    void FinishCapturing()
    {
        m_Capturing = false;
        ChangeColor();
    }

    void ChangeColor()
    {
        if (m_Capturing)
        {
            gameObject.GetComponentInChildren<Renderer>().material = goodMaterial;
        }
        else
        {
            gameObject.GetComponentInChildren<Renderer>().material = normalMaterial;    
        }
    }

    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        MoveAgent(actionBuffers);
        AddReward(existentialPenalty);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActionsOut = actionsOut.ContinuousActions;
        if (Input.GetKey(KeyCode.D))
        {
            continuousActionsOut[2] = 1;
        }
        if (Input.GetKey(KeyCode.W))
        {
            continuousActionsOut[0] = 1;
        }
        if (Input.GetKey(KeyCode.A))
        {
            continuousActionsOut[2] = -1;
        }
        if (Input.GetKey(KeyCode.S))
        {
            continuousActionsOut[0] = -1;
        }
        var discreteActionsOut = actionsOut.DiscreteActions;
        discreteActionsOut[0] = Input.GetKey(KeyCode.Space) ? 1 : 0;
    }

    public override void OnEpisodeBegin()
    {
        m_Shoot = false;
        m_AgentRb.linearVelocity = Vector3.zero;
        myLaser.transform.localScale = new Vector3(0f, 0f, 0f);
        transform.position = new Vector3(Random.Range(-m_MyArea.range, m_MyArea.range),
            2f, Random.Range(-m_MyArea.range, m_MyArea.range))
            + area.transform.position;
        transform.rotation = Quaternion.Euler(new Vector3(0f, Random.Range(0, 360)));

        SetResetParameters();
    }

    void OnCollisionEnter(Collision collision)
    {
        var prey = collision.gameObject.GetComponent<PreyAgent>();
        if (prey != null)
        {
            Capture();
        }
    }

    public void SetLaserLengths()
    {
        m_LaserLength = m_ResetParams.GetWithDefault("laser_length", 1.0f);
    }

    public void SetAgentScale()
    {
        float agentScale = m_ResetParams.GetWithDefault("agent_scale", 1.0f);
        gameObject.transform.localScale = new Vector3(agentScale, agentScale, agentScale);
    }

    public void SetResetParameters()
    {
        SetLaserLengths();
        SetAgentScale();
    }
}
