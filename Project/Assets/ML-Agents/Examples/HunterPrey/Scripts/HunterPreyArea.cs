using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.MLAgentsExamples;

public class HunterPreyArea : Area
{
    public GameObject food;
    public GameObject badFood;
    public int numFood;
    public int numBadFood;
    public bool respawnFood;
    public float range;
    public List<HunterAgent> hunters = new List<HunterAgent>();
    public List<PreyAgent> prey = new List<PreyAgent>();

    void Start()
    {
        hunters = GetComponentsInChildren<HunterAgent>().ToList();
        prey = GetComponentsInChildren<PreyAgent>().ToList();
    }

    void CreateFood(int num, GameObject type)
    {
        for (int i = 0; i < num; i++)
        {
            GameObject f = Instantiate(type, new Vector3(Random.Range(-range, range), 1f,
                Random.Range(-range, range)) + transform.position,
                Quaternion.Euler(new Vector3(0f, Random.Range(0f, 360f), 90f)));
            f.GetComponent<PlantLogic>().respawn = respawnFood;
            f.GetComponent<PlantLogic>().myArea = this;
        }
    }

    public void ResetFoodArea(GameObject[] agents)
    {
        foreach (GameObject agent in agents)
        {
            if (agent.transform.parent == gameObject.transform)
            {
                agent.transform.position = new Vector3(Random.Range(-range, range), 2f,
                    Random.Range(-range, range))
                    + transform.position;
                agent.transform.rotation = Quaternion.Euler(new Vector3(0f, Random.Range(0, 360)));
            }
        }

        CreateFood(numFood, food);
        CreateFood(numBadFood, badFood);
    }

    public override void ResetArea()
    {
    }

    public void OnPreyCaptured(float groupReward)
    {
        foreach (var hunter in hunters)
        {
            hunter.AddReward(groupReward);
        }
    }
}
