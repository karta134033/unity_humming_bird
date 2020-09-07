using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FlowerArea : MonoBehaviour {
    public const float AreaDiameter = 20f;  // 用來觀察agnet跟flower的相對距離
    private List<GameObject> flowerPlants;  // 因為flower plants可以有許多flower plants
    private Dictionary<Collider, Flower> nectarFlowerDictionary;  // 用來以collider找flower

    public List<Flower> Flowers { get; private set; }  // 儲存在flower area的所有flower

    public void ResetFlowers() {  // 重置flower和flower plants
        foreach (GameObject flowerPlant in flowerPlants) {
            float xRotation = UnityEngine.Random.Range(-5f, 5f);
            float yRotation = UnityEngine.Random.Range(-180f, 180f);
            float zRotation = UnityEngine.Random.Range(-5f, 5f);
            flowerPlant.transform.localRotation = Quaternion.Euler(xRotation, yRotation, zRotation);  // 隨機旋轉x y z
        }

        foreach (Flower flower in Flowers) {
            flower.ResetFlower();
        }
    }

    public Flower GetFlowerFromNectar(Collider collider) {  // 以nectar collider來找flower
        return nectarFlowerDictionary[collider];
    }

    private void Awake() {
        flowerPlants = new List<GameObject>();
        nectarFlowerDictionary = new Dictionary<Collider, Flower>();
        Flowers = new List<Flower>();
    }

    private void Start() {  // 找出所有屬於此GameObject/Transform的flowers
        FindChildFlowers(transform);
    }

    private void FindChildFlowers(Transform parent) {
        for (int i = 0; i < parent.childCount; i++) {
            Transform child = parent.GetChild(i);
            if (child.CompareTag("flower_plant")) {
                flowerPlants.Add(child.gameObject);

                FindChildFlowers(child);
            } else {
                Flower flower = child.GetComponent<Flower>();
                if (flower != null) {
                    Flowers.Add(flower);
                    nectarFlowerDictionary.Add(flower.nectarCollider, flower);
                } else {
                    FindChildFlowers(child);
                }
            }
        }
    }

}
