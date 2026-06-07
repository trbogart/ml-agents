using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;
using System.Diagnostics;

public class PreyAgent : Agent
{
    HunterPreySettings m_HunterPreySettings;
    public GameObject area;
    HunterPreyArea m_MyArea;
    bool m_Frozen;
    bool m_Poisoned;
    bool m_Satiated;
    float m_FrozenTime;
    float m_PoisonedTime;
    float m_SatiatedTime;
    Rigidbody m_AgentRb;
    // Speed of agent rotation.
    public float turnSpeed = 300;

    // Speed of agent movement.
    public float moveSpeed = 2;
    public Material normalMaterial;
    public Material badMaterial;
    public Material goodMaterial;
    public Material frozenMaterial;
    public bool useVectorObs; // TODO delete
    [Tooltip("Use only the frozen flag in vector observations. If \"Use Vector Obs\" " +
             "is checked, this option has no effect. This option is necessary for the " +
             "VisualHunterPrey scene.")]
    public bool useVectorFrozenFlag; // TODO delete
    public float foodReward = 1;
    public float poisonPenalty = -1;
    public float frozenPenalty = -1;
    public float eatenPenalty = -2;
    public float existentialPenalty = -0.0001f;
    public float frozenDuration = 10;
    public float satiatedDuration = 1;
    public float poisonedDuration = 2;

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
            sensor.AddObservation(m_Frozen);
        }
        else if (useVectorFrozenFlag)
        {
            sensor.AddObservation(m_Frozen);
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
        if (m_Frozen && Time.time > m_FrozenTime + frozenDuration)
        {
            Unfreeze();
        }
        if (m_Poisoned && Time.time > m_PoisonedTime + poisonedDuration)
        {
            Unpoison();
        }
        if (m_Satiated && Time.time > m_SatiatedTime + satiatedDuration)
        {
            Unsatiate();
        }

        var dirToGo = Vector3.zero;
        var rotateDir = Vector3.zero;

        var continuousActions = actionBuffers.ContinuousActions;

        if (!m_Frozen && !m_Poisoned && !m_Satiated)
        {
            var forward = Mathf.Clamp(continuousActions[0], -1f, 1f);
            var right = Mathf.Clamp(continuousActions[1], -1f, 1f);
            var rotate = Mathf.Clamp(continuousActions[2], -1f, 1f);

            dirToGo = transform.forward * forward;
            dirToGo += transform.right * right;
            rotateDir = -transform.up * rotate;

            m_AgentRb.AddForce(dirToGo * moveSpeed, ForceMode.VelocityChange);
            transform.Rotate(rotateDir, Time.fixedDeltaTime * turnSpeed);
        }

        if (m_AgentRb.linearVelocity.sqrMagnitude > 25f) // slow it down
        {
            m_AgentRb.linearVelocity *= 0.95f;
        }
    }

    
    public void OnEaten()
    {
        AddReward(eatenPenalty);   
        OnSpawn();
    }

    public void Freeze()
    {
        AddReward(frozenPenalty);   
        gameObject.tag = "frozenPrey";
        m_Frozen = true;
        m_FrozenTime = Time.time;
        gameObject.GetComponentInChildren<Renderer>().material = frozenMaterial;
    }

    void Unfreeze()
    {
        m_Frozen = false;
        gameObject.tag = "prey";
        gameObject.GetComponentInChildren<Renderer>().material = normalMaterial;
    }

    void Poison()
    {
        AddReward(poisonPenalty);
        m_Poisoned = true;
        m_PoisonedTime = Time.time;
        gameObject.GetComponentInChildren<Renderer>().material = badMaterial;
    }

    void Unpoison()
    {
        m_Poisoned = false;
        gameObject.GetComponentInChildren<Renderer>().material = normalMaterial;
    }

    void Satiate()
    {
        AddReward(foodReward);
        m_Satiated = true;
        m_SatiatedTime = Time.time;
        gameObject.GetComponentInChildren<Renderer>().material = goodMaterial;
    }

    void Unsatiate()
    {
        m_Satiated = false;
        gameObject.GetComponentInChildren<Renderer>().material = normalMaterial;
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

    private void OnSpawn()
    {
        Unfreeze();
        Unpoison();
        Unsatiate();
        m_AgentRb.linearVelocity = Vector3.zero;
        transform.position = new Vector3(Random.Range(-m_MyArea.range, m_MyArea.range),
            2f, Random.Range(-m_MyArea.range, m_MyArea.range))
            + area.transform.position;
        transform.rotation = Quaternion.Euler(new Vector3(0f, Random.Range(0, 360)));
    }

    public override void OnEpisodeBegin()
    {
        OnSpawn();
        SetResetParameters();
    }

    void OnCollisionEnter(Collision collision)
    {
        Debug.Log($"Prey collides with {collision.gameObject.tag}");
        if (collision.gameObject.CompareTag("food"))
        {
            Satiate();
            collision.gameObject.GetComponent<PlantLogic>().OnEaten();
        }
        if (collision.gameObject.CompareTag("badFood"))
        {
            Poison();
            collision.gameObject.GetComponent<PlantLogic>().OnEaten();
        }
        if (collision.gameObject.CompareTag("hunter"))
        {
            OnEaten();
        }
    }

    public void SetAgentScale()
    {
        float agentScale = m_ResetParams.GetWithDefault("agent_scale", 1.0f);
        gameObject.transform.localScale = new Vector3(agentScale, agentScale, agentScale);
    }

    public void SetResetParameters()
    {
        SetAgentScale();
    }
}
