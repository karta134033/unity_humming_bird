using System;
using System.Collections;
using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using UnityEngine;

public class HummingBirdAgent : Agent {  // 使用ml-agent 需要繼承Agent
    public float moveForce = 2f;
    public float pitchSpeed = 100f;  // 將鳥上下轉動
    public float yawSpeed = 100f;  // 將鳥左右轉動
    public Transform beakTip; // 鳥喙前端的transform
    public Camera agentCamera;

    public bool trainingMode;  // 看是否由ml-agent掌控

    new private Rigidbody rigidbody;  // agent的rigidbody
    private FlowerArea flowerArea;  // agent進入的flower area
    private Flower nearestFlower;
    private float smoothPitchChange = 0f;  // 讓動作更加自然
    private float smoothYawChange = 0f;
    private const float MaxPitchAngle = 80f;
    private const float BeakTipRadius = 0.008f;
    private bool frozen = false;

    public float NectarObtained { get; private set; }

    public override void Initialize() {  // 複寫agent class的方法
        rigidbody = GetComponent<Rigidbody>();
        flowerArea = GetComponentInParent<FlowerArea>();

        if (!trainingMode) MaxStep = 0;  // 如果不是訓練模式則不會設定最大的步數
    }

    public override void OnEpisodeBegin() {
        if (trainingMode) {  // Only reset flower in training when there is one agent per area
            flowerArea.ResetFlowers();
        }

        NectarObtained = 0f;
        rigidbody.velocity = Vector3.zero;  // 在新的episode開始前歸零速度
        rigidbody.angularVelocity = Vector3.zero;
        bool inFrontOfFlower = true;
        if (trainingMode) {
            inFrontOfFlower = UnityEngine.Random.value > .5f;  // 五成的機率會inFrontOfFlower
        }
        MoveToSafeRandomPosition(inFrontOfFlower);  // 隨機移動agent到新的路徑
        UpdateNearestFlower();  // 重新計算隨機移動agent後距離最近的花朵

    }

    /// <summary>
    /// vectorAction 代表
    /// Index 0: 移動vector x (+1 = right, -1 = left)
    /// Index 1: 移動vector y (+1 = up, -1 = down)
    /// Index 2: 移動vector z (+1 = forword, -1 = backward)
    /// Index 3: pitch angle  (+1 = pitch up, -1 = pitch down)
    /// Index 4: yaw angle    (+1 = turn right, -1 = turn left)
    /// </summary>
    /// <parm name="vectorAction">The actions to take</parm>
    public override void OnActionReceived(float[] vectorAction) {
        if (frozen) return;
        Vector3 move = new Vector3(vectorAction[0], vectorAction[1], vectorAction[2]);
        rigidbody.AddForce(move * moveForce);  // Add force in the direction of the move vector
        Vector3 rotationVector = transform.rotation.eulerAngles;  // 取得現在的rotation
        float pitchChange = vectorAction[3];
        float yawChange = vectorAction[4];
        smoothPitchChange = Mathf.MoveTowards(smoothPitchChange, pitchChange, 2f * Time.fixedDeltaTime);  // 讓轉換更流暢
        smoothYawChange = Mathf.MoveTowards(smoothYawChange, yawChange, 2f * Time.fixedDeltaTime);

        float pitch = rotationVector.x + smoothPitchChange * Time.fixedDeltaTime * pitchSpeed;
        if (pitch > 180f) pitch -= 360f;  // 避免轉過頭造成鳥上下顛倒
        pitch = Mathf.Clamp(pitch, -MaxPitchAngle, MaxPitchAngle);
        float yaw = rotationVector.y + smoothYawChange * Time.fixedDeltaTime * yawSpeed;

        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    /// <summary>
    /// 神經網路收集環境取得的資訊，產生對應的action
    /// </summary>
    public override void CollectObservations(VectorSensor sensor) {
        if (nearestFlower == null) {
            sensor.AddObservation(new float[10]);  // 特殊情況下會沒有最近的花，但還是接收資訊(共10個observations)，並設為0
            return;
        }
        // 共10個observations
        sensor.AddObservation(transform.localRotation.normalized);  // 4個observations
        Vector3 toFlower = nearestFlower.FlowerCenterPosition - beakTip.position;  // 取得鳥喙到最近花朵的vector
        sensor.AddObservation(toFlower.normalized);  // 3個observations
        sensor.AddObservation(Vector3.Dot(toFlower.normalized, -nearestFlower.FlowerUpVector.normalized)); // +1代表鳥喙正對花 -1代表背對  1個observation
        sensor.AddObservation(Vector3.Dot(beakTip.forward.normalized, -nearestFlower.FlowerUpVector.normalized)); // +1代表鳥喙朝向花 -1代表背向  1個observation
        sensor.AddObservation(toFlower.magnitude / FlowerArea.AreaDiameter);  // beakTip到flower的相對距離  1個observation
    }

    /// <summary>
    /// 只有在agent's Behavior Parmeters 的 "Behavior Type"設定為 "Heuristic Only"時，這個function才會被呼叫
    /// 輸出的action會傳入OnActionReceived
    /// </summary>
    public override void Heuristic(float[] actionsOut) {
        Vector3 forword = Vector3.zero;
        Vector3 left = Vector3.zero;
        Vector3 up = Vector3.zero;
        float pitch = 0f;
        float yaw = 0f;
        if (Input.GetKey(KeyCode.W)) forword = transform.forward;
        else if (Input.GetKey(KeyCode.S)) forword = -transform.forward;

        if (Input.GetKey(KeyCode.A)) left = -transform.right;
        else if (Input.GetKey(KeyCode.D)) left = transform.right;

        if (Input.GetKey(KeyCode.E)) up = -transform.up;
        else if (Input.GetKey(KeyCode.Q)) up = transform.up;

        if (Input.GetKey(KeyCode.DownArrow)) pitch = 1f;
        else if (Input.GetKey(KeyCode.UpArrow)) pitch = -1f;

        if (Input.GetKey(KeyCode.LeftArrow)) yaw = -1f;
        else if (Input.GetKey(KeyCode.RightArrow)) yaw = 1f;

        Vector3 combined = (forword + left + up).normalized;
        actionsOut[0] = combined.x;
        actionsOut[1] = combined.y;
        actionsOut[2] = combined.z;
        actionsOut[3] = pitch;
        actionsOut[4] = yaw;
    }
    
    public void FreezeAgent() {  // Prevent the agent from moving and taking actions
        Debug.Assert(trainingMode == false, "Freeze/Unfreeze not supported in training");
        frozen = false;
        rigidbody.WakeUp();
    }


    private void MoveToSafeRandomPosition(bool inFrontOfFlower) {  // 移動到安全的位置， inFrontOfFlower決定是否要移動到flower前方
        bool safePositionFound = false;
        int attemptsRemaining = 100;  // 避免無窮迴圈
        Vector3 potentialPosition = Vector3.zero;
        Quaternion potentialRotation = new Quaternion();

        while(!safePositionFound && attemptsRemaining > 0) {
            attemptsRemaining--;
            if (inFrontOfFlower) {  // 隨機選取一個flower
                Flower randomFlower = flowerArea.Flowers[UnityEngine.Random.Range(0, flowerArea.Flowers.Count)];
                float distanceFromFlower = UnityEngine.Random.Range(.1f, .2f);  // 放在flower 10~20公分前
                potentialPosition = randomFlower.transform.position + randomFlower.FlowerUpVector * distanceFromFlower;
                
                Vector3 toFlower = randomFlower.FlowerCenterPosition - potentialPosition;
                potentialRotation = Quaternion.LookRotation(toFlower, Vector3.up);  // 將鳥轉向flower
            } else {
                float height = UnityEngine.Random.Range(1.2f, 2.5f);  // 隨機選取高度
                float radius = UnityEngine.Random.Range(2f, 7f);  // 以圖為中心隨機選取半徑

                Quaternion direction = Quaternion.Euler(0f, UnityEngine.Random.Range(-180f, 180f), 0f);
                potentialPosition = flowerArea.transform.position + Vector3.up * height + direction * Vector3.forward * radius;
                float pitch = UnityEngine.Random.Range(-60f, 60f);  // 隨機選取設定pitch
                float yaw = UnityEngine.Random.Range(-180f, 180f);  // 隨機選取設定yaw
                potentialRotation = Quaternion.Euler(pitch, yaw, 0f);
            }

            Collider[] colliders = Physics.OverlapSphere(potentialPosition, 0.05f);  // 檢查是否有撞到collider
            safePositionFound = colliders.Length == 0;
        }

        Debug.Assert(safePositionFound, "Could not find a safe position to spawn");
        transform.position = potentialPosition;
        transform.rotation = potentialRotation;
    }

    private void UpdateNearestFlower() {  // 告知bird最近的花，但不持續更新，避免動作錯亂
        foreach (Flower flower in flowerArea.Flowers) {
            if (nearestFlower == null && flower.HasNectar) {
                nearestFlower = flower;
            } 
            else if (flower.HasNectar) {  // 比較這朵花與nearestFlower和鳥喙的距離
                float distanceToFlower = Vector3.Distance(flower.transform.position, beakTip.position);
                float distanceToCurrentNearestFlower = Vector3.Distance(nearestFlower.transform.position, beakTip.position);

                if (!nearestFlower.HasNectar || distanceToFlower < distanceToCurrentNearestFlower) {
                    nearestFlower = flower;
                }
            }
        }
    }

    private void OnTriggerEnter(Collider other) {  // 當agent's colliders 觸發trigger collider
        TriggerEnterOrStay(other);
    }

    private void OnTriggerStay(Collider other) {  // 當agent's colliders 停留在trigger collider
        TriggerEnterOrStay(other);
    }

    private void TriggerEnterOrStay(Collider collider) {  // collider帶入的是other的
        if (collider.CompareTag("nectar")) {  // 檢查agent是否collide到花蜜
            Vector3 closestPointToBeakTip = collider.ClosestPoint(beakTip.position);

            if (Vector3.Distance(beakTip.position, closestPointToBeakTip) < BeakTipRadius) {
                Flower flower = flowerArea.GetFlowerFromNectar(collider);
                float nectarReceived = flower.Feed(.01f);
                NectarObtained += nectarReceived;

                if (trainingMode) {
                    float bonus = .02f * Mathf.Clamp01(Vector3.Dot(transform.forward.normalized, -nearestFlower.FlowerUpVector.normalized));  // 計算取得nectar的reward
                    AddReward(.01f + bonus);
                }

                if (!flower.HasNectar) {  // 如果沒有花蜜才會更新最近的花
                    UpdateNearestFlower();
                }
            }
        }

    }

    private void OnCollisionEnter(Collision collision) {  // Called when the agent collides with something solid
        if (trainingMode && collision.collider.CompareTag("boundary")) {
            AddReward(-.5f);  // 撞到邊界給予負的回饋 但不會太多
        }
    }

    private void Update() {  // 每一幀，執行一次
        if (nearestFlower != null) {  // 畫出鳥喙到最近的花
            Debug.DrawLine(beakTip.position, nearestFlower.FlowerCenterPosition, Color.green);
        }
    }

    private void FixedUpdate() {  // 每0.02秒執行一次
        // 避免特殊情況: 當agent目標最近的花的花蜜都被搶走但沒有更新花，則agent會永遠專注在那朵花
        if (nearestFlower != null && !nearestFlower.HasNectar) {
            UpdateNearestFlower();
        }
    }
}
