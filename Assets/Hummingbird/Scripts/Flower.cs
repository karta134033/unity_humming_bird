using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Flower : MonoBehaviour {
    public Color fullFlowerColor = new Color(1f, 0f, .3f);
    public Color emptyFlowerColor = new Color(.5f, 0f, 1f);
    public Collider nectarCollider;
    private Collider flowerCollider;
    private Material flowerMaterial;

    public Vector3 FlowerUpVector {
        get {
            return nectarCollider.transform.up;
        }
    }

    public Vector3 FlowerCenterPosition {
        get {
            return nectarCollider.transform.position;
        }
    }
    
    public float NectarAmount { get; private set; }

    public bool HasNectar {
        get {
            return NectarAmount > 0f;
        }
    }

    public float Feed(float amount) {
        float nectarTaken = Mathf.Clamp(amount, 0f, NectarAmount);  // 紀錄有多少花蜜被成功拿取(不可拿超過現有數量)
        NectarAmount -= amount;

        if (NectarAmount <= 0) {
            NectarAmount = 0;
            flowerCollider.gameObject.SetActive(false);  // disable 花跟花蜜的collider
            nectarCollider.gameObject.SetActive(false);

            flowerMaterial.SetColor("_BaseColor", emptyFlowerColor);  // 改變花色，代表它已為空
        }
        return nectarTaken;
    }

    public void ResetFlower() {
        NectarAmount = 1f;
        flowerCollider.gameObject.SetActive(true);
        nectarCollider.gameObject.SetActive(true);
        flowerMaterial.SetColor("_BaseColor", fullFlowerColor);
    }

    private void Awake() {  // 當花被喚醒時會呼叫此方法
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();  // 取得花的MeshRenderer
        flowerMaterial = meshRenderer.material;
        flowerCollider = transform.Find("FlowerCollider").GetComponent<Collider>();  // 找到Collider並assign過去
        nectarCollider = transform.Find("FlowerNectarCollider").GetComponent<Collider>();
    }
}
