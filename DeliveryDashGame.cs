using UnityEngine;

/// <summary>
/// Builds and runs the Delivery Dash arcade driving game.
/// </summary>
public sealed class DeliveryDashGame : MonoBehaviour
{
    private const int DeliveryGoal = 5;
    private const float ShiftDuration = 180f;
    private const float DeliveryTimeBonus = 15f;
    private const float InteractionDistance = 2.7f;
    private const float CityHalfSize = 41f;

    private static readonly Vector3[] MissionNodes =
    {
        new Vector3(-24f, 0f, -30f),
        new Vector3(0f, 0f, -28f),
        new Vector3(24f, 0f, -30f),
        new Vector3(-30f, 0f, -24f),
        new Vector3(30f, 0f, -24f),
        new Vector3(-30f, 0f, 0f),
        new Vector3(30f, 0f, 0f),
        new Vector3(-30f, 0f, 24f),
        new Vector3(30f, 0f, 24f),
        new Vector3(-24f, 0f, 30f),
        new Vector3(0f, 0f, 30f),
        new Vector3(24f, 0f, 30f)
    };

    private readonly Vector3 playerStart = new Vector3(0f, 0.02f, -35f);

    private DeliveryDashVehicle vehicle;
    private Camera followCamera;
    private GameObject targetMarker;
    private Material grassMaterial;
    private Material roadMaterial;
    private Material sidewalkMaterial;
    private Material roadLineMaterial;
    private Material[] buildingMaterials;
    private Material windowMaterial;
    private Material pickupMaterial;
    private Material deliveryMaterial;
    private Material parcelMaterial;

    private int deliveries;
    private int currentNodeIndex = -1;
    private int previousNodeIndex = -1;
    private float timeRemaining;
    private bool carryingParcel;
    private bool gameOver;
    private bool completedShift;
    private string statusMessage = "Find the highlighted parcel.";
    private float lastCollisionMessageTime = -10f;

    private void Awake()
    {
        Application.targetFrameRate = 60;
        CreateMaterials();
        BuildCity();
        BuildPlayer();
        SetupCamera();
        SetupLighting();
        RestartGame();
    }

    private void Update()
    {
        if (gameOver)
        {
            return;
        }

        timeRemaining -= Time.deltaTime;
        if (timeRemaining <= 0f)
        {
            timeRemaining = 0f;
            FinishShift(false);
            return;
        }

        if (vehicle == null || targetMarker == null)
        {
            return;
        }

        Vector3 playerPosition = vehicle.transform.position;
        Vector3 targetPosition = MissionNodes[currentNodeIndex];
        playerPosition.y = 0f;
        targetPosition.y = 0f;

        if (Vector3.Distance(playerPosition, targetPosition) <= InteractionDistance)
        {
            if (carryingParcel)
            {
                CompleteDelivery();
            }
            else
            {
               PickUpParcel();
            }
        }
    }

    private void LateUpdate()
    {
        if (followCamera != null && vehicle != null)
        {
            Vector3 desiredPosition = vehicle.transform.position - vehicle.transform.forward * 13f + Vector3.up * 21f;
            followCamera.transform.position = Vector3.Lerp(followCamera.transform.position, desiredPosition, Time.deltaTime * 4f);
            Vector3 lookTarget = vehicle.transform.position + vehicle.transform.forward * 5f + Vector3.up * 0.3f;
            followCamera.transform.LookAt(lookTarget);
        }

        if (targetMarker != null)
        {
            Vector3 markerPosition = MissionNodes[currentNodeIndex];
            markerPosition.y = 0.15f + Mathf.Sin(Time.time * 3f) * 0.13f;
            targetMarker.transform.position = markerPosition;
            targetMarker.transform.Rotate(Vector3.up, Time.deltaTime * 35f, Space.World);
        }
    }

    private void OnGUI()
    {
        const float panelWidth = 340f;
        const float panelHeight = 178f;
        GUI.Box(new Rect(18f, 18f, panelWidth, panelHeight), GUIContent.none);

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 24,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(1f, 0.72f, 0.25f) }
        };
        GUIStyle bodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };
        GUIStyle hintStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            normal = { textColor = new Color(0.82f, 0.88f, 0.93f) }
        };

        GUI.Label(new Rect(34f, 26f, 300f, 32f), "DELIVERY DASH", titleStyle);
        GUI.Label(new Rect(34f, 64f, 300f, 24f), "DELIVERIES   " + deliveries + " / " + DeliveryGoal, bodyStyle);
        GUI.Label(new Rect(34f, 91f, 300f, 24f), "TIME             " + FormatTime(timeRemaining), bodyStyle);
        GUI.Label(new Rect(34f, 119f, 300f, 24f), statusMessage, hintStyle);
        GUI.Label(new Rect(34f, 145f, 310f, 34f), "WASD / arrows: drive    Space: brake    R: restart", hintStyle);

        if (!gameOver)
        {
            return;
        }

        float overlayWidth = 380f;
        float overlayHeight = 150f;
        Rect overlay = new Rect((Screen.width - overlayWidth) * 0.5f, (Screen.height - overlayHeight) * 0.5f, overlayWidth, overlayHeight);
        GUI.Box(overlay, GUIContent.none);
        GUIStyle resultStyle = new GUIStyle(titleStyle)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 27
        };
        string result = completedShift ? "SHIFT COMPLETE!" : "TIME'S UP!";
        GUI.Label(new Rect(overlay.x + 12f, overlay.y + 15f, overlayWidth - 24f, 42f), result, resultStyle);
        GUI.Label(new Rect(overlay.x + 12f, overlay.y + 59f, overlayWidth - 24f, 25f), "You delivered " + deliveries + " parcel" + (deliveries == 1 ? "." : "s."), bodyStyle);
        if (GUI.Button(new Rect(overlay.x + 105f, overlay.y + 98f, 170f, 34f), "START NEW SHIFT"))
        {
            RestartGame();
        }
    }

    /// <summary>
    /// Restarts the shift, resetting the timer, score, vehicle, and current parcel route.
    /// </summary>
    public void RestartGame()
    {
        deliveries = 0;
        timeRemaining = ShiftDuration;
        carryingParcel = false;
        gameOver = false;
        completedShift = false;
        previousNodeIndex = -1;
        statusMessage = "Find the highlighted parcel.";

        if (targetMarker != null)
        {
            Destroy(targetMarker);
            targetMarker = null;
        }

        if (vehicle != null)
        {
            vehicle.ResetVehicle(playerStart, 0f);
            vehicle.SetCanDrive(true);
        }

        BeginNextPickup();
    }

    /// <summary>
    /// Displays a short feedback message when the delivery vehicle bumps an obstacle.
    /// </summary>
    public void NotifyCollision()
    {
        if (Time.time - lastCollisionMessageTime < 1.1f || gameOver)
        {
            return;
        }

        lastCollisionMessageTime = Time.time;
        statusMessage = "Bump! Keep to the road.";
    }

    private void CreateMaterials()
    {
        grassMaterial = MakeMaterial("Grass", new Color(0.23f, 0.48f, 0.29f));
        roadMaterial = MakeMaterial("Warm asphalt", new Color(0.16f, 0.19f, 0.23f));
        sidewalkMaterial = MakeMaterial("Concrete", new Color(0.58f, 0.61f, 0.59f));
        roadLineMaterial = MakeMaterial("Road paint", new Color(0.97f, 0.81f, 0.28f));
        windowMaterial = MakeMaterial("Blue windows", new Color(0.31f, 0.66f, 0.79f));
        pickupMaterial = MakeMaterial("Pickup gold", new Color(1f, 0.68f, 0.12f), true);
        deliveryMaterial = MakeMaterial("Delivery cyan", new Color(0.12f, 0.89f, 0.94f), true);
        parcelMaterial = MakeMaterial("Parcel", new Color(0.76f, 0.40f, 0.17f));
        buildingMaterials = new[]
        {
            MakeMaterial("Coral building", new Color(0.79f, 0.39f, 0.29f)),
            MakeMaterial("Sand building", new Color(0.82f, 0.66f, 0.40f)),
            MakeMaterial("Blue building", new Color(0.36f, 0.55f, 0.68f)),
            MakeMaterial("Lavender building", new Color(0.59f, 0.48f, 0.68f)),
            MakeMaterial("Mint building", new Color(0.39f, 0.66f, 0.52f))
        };
    }

    private Material MakeMaterial(string materialName, Color color, bool emissive = false)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        Material material = new Material(shader)
        {
            name = materialName,
            color = color
        };
        if (emissive)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 1.3f);
        }
        return material;
    }

    private void BuildCity()
    {
        CreateCube("City Ground", new Vector3(0f, -0.36f, 0f), new Vector3(84f, 0.5f, 84f), grassMaterial, true);

        float[] streetCenters = { -24f, 0f, 24f };
        for (int i = 0; i < streetCenters.Length; i++)
        {
            float center = streetCenters[i];
            CreateCube("North-South Road", new Vector3(center, -0.015f, 0f), new Vector3(9f, 0.1f, 82f), roadMaterial, false);
            CreateCube("East-West Road", new Vector3(0f, -0.015f, center), new Vector3(82f, 0.1f, 9f), roadMaterial, false);

            for (int side = -1; side <= 1; side += 2)
            {
                CreateCube("Sidewalk", new Vector3(center + side * 5.25f, 0.02f, 0f), new Vector3(1.4f, 0.12f, 82f), sidewalkMaterial, false);
                CreateCube("Sidewalk", new Vector3(0f, 0.02f, center + side * 5.25f), new Vector3(82f, 0.12f, 1.4f), sidewalkMaterial, false);
            }

            for (float mark = -36f; mark <= 36f; mark += 5.5f)
            {
                CreateCube("Center line", new Vector3(center, 0.045f, mark), new Vector3(0.16f, 0.025f, 2f), roadLineMaterial, false);
                CreateCube("Center line", new Vector3(mark, 0.045f, center), new Vector3(2f, 0.025f, 0.16f), roadLineMaterial, false);
            }
        }

        BuildBuildings();
        BuildCrosswalks(streetCenters);
        BuildCityBounds();
    }

    private void BuildBuildings()
    {
        int buildingIndex = 0;
        for (int xSign = -1; xSign <= 1; xSign += 2)
        {
            for (int zSign = -1; zSign <= 1; zSign += 2)
            {
                float blockX = xSign * 12f;
                float blockZ = zSign * 12f;
                BuildBuilding("Corner shop", new Vector3(blockX - xSign * 3.4f, 0f, blockZ + zSign * 3f), 5.4f, 5.4f, 5.5f + buildingIndex % 3 * 1.4f, buildingIndex++);
                BuildBuilding("Apartment", new Vector3(blockX + xSign * 3.1f, 0f, blockZ - zSign * 3.3f), 5.4f, 5.4f, 7f + buildingIndex % 3 * 1.5f, buildingIndex++);
            }
        }
    }

    private void BuildBuilding(string buildingName, Vector3 groundPosition, float width, float depth, float height, int materialIndex)
    {
        Material buildingMaterial = buildingMaterials[materialIndex % buildingMaterials.Length];
        CreateCube(buildingName, groundPosition + Vector3.up * (height * 0.5f), new Vector3(width, height, depth), buildingMaterial, true);
        CreateCube("Flat roof", groundPosition + Vector3.up * (height + 0.12f), new Vector3(width + 0.4f, 0.25f, depth + 0.4f), buildingMaterial, false);

        float frontZ = groundPosition.z + depth * 0.5f + 0.025f;
        for (int windowRow = 0; windowRow < 2; windowRow++)
        {
            float windowY = Mathf.Min(1.55f + windowRow * 1.75f, height - 0.9f);
            for (int windowColumn = -1; windowColumn <= 1; windowColumn++)
            {
                CreateCube("Shop window", new Vector3(groundPosition.x + windowColumn * 1.55f, windowY, frontZ), new Vector3(0.75f, 0.9f, 0.08f), windowMaterial, false);
            }
        }
    }

    private void BuildCrosswalks(float[] streetCenters)
    {
        for (int xIndex = 0; xIndex < streetCenters.Length; xIndex++)
        {
            for (int zIndex = 0; zIndex < streetCenters.Length; zIndex++)
            {
                float x = streetCenters[xIndex];
                float z = streetCenters[zIndex];
                for (int stripe = -2; stripe <= 2; stripe++)
                {
                    CreateCube("Crosswalk stripe", new Vector3(x + stripe * 1.25f, 0.047f, z - 5.1f), new Vector3(0.72f, 0.025f, 0.35f), sidewalkMaterial, false);
                    CreateCube("Crosswalk stripe", new Vector3(x - 5.1f, 0.047f, z + stripe * 1.25f), new Vector3(0.35f, 0.025f, 0.72f), sidewalkMaterial, false);
                }
            }
        }
    }

    private void BuildCityBounds()
    {
        CreateCube("North city barrier", new Vector3(0f, 1f, CityHalfSize), new Vector3(84f, 2f, 1f), roadMaterial, true);
        CreateCube("South city barrier", new Vector3(0f, 1f, -CityHalfSize), new Vector3(84f, 2f, 1f), roadMaterial, true);
        CreateCube("East city barrier", new Vector3(CityHalfSize, 1f, 0f), new Vector3(1f, 2f, 84f), roadMaterial, true);
        CreateCube("West city barrier", new Vector3(-CityHalfSize, 1f, 0f), new Vector3(1f, 2f, 84f), roadMaterial, true);
    }

    private GameObject CreateCube(string objectName, Vector3 position, Vector3 scale, Material material, bool hasCollider)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = objectName;
        cube.transform.position = position;
        cube.transform.localScale = scale;
        cube.GetComponent<Renderer>().sharedMaterial = material;
        if (!hasCollider)
        {
            Collider objectCollider = cube.GetComponent<Collider>();
            if (objectCollider != null)
            {
                Destroy(objectCollider);
            }
        }
        return cube;
    }

    private void BuildPlayer()
    {
        GameObject car = new GameObject("Delivery Scooter");
        car.transform.position = playerStart;
        Rigidbody body = car.AddComponent<Rigidbody>();
        body.useGravity = false;
        body.mass = 700f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        BoxCollider carCollider = car.AddComponent<BoxCollider>();
        carCollider.center = new Vector3(0f, 0.48f, 0f);
        carCollider.size = new Vector3(1.35f, 0.75f, 2.35f);

        Material bodyMaterial = MakeMaterial("Scooter orange", new Color(0.96f, 0.36f, 0.1f));
        Material tireMaterial = MakeMaterial("Rubber", new Color(0.07f, 0.09f, 0.11f));
        Material lightMaterial = MakeMaterial("Headlights", new Color(1f, 0.88f, 0.45f), true);
        Material tailMaterial = MakeMaterial("Tail lights", new Color(0.95f, 0.13f, 0.09f), true);

        CreateCarPart(car.transform, "Scooter body", new Vector3(0f, 0.46f, 0f), new Vector3(1.35f, 0.48f, 2.35f), bodyMaterial);
        CreateCarPart(car.transform, "Cabin", new Vector3(0f, 0.84f, -0.12f), new Vector3(1.02f, 0.42f, 1.2f), windowMaterial);
        CreateCarPart(car.transform, "Front cowl", new Vector3(0f, 0.62f, 0.82f), new Vector3(1.18f, 0.3f, 0.72f), bodyMaterial);

        for (int side = -1; side <= 1; side += 2)
        {
            for (int axle = -1; axle <= 1; axle += 2)
            {
                GameObject wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                wheel.name = "Scooter wheel";
                wheel.transform.SetParent(car.transform, false);
                wheel.transform.localPosition = new Vector3(side * 0.68f, 0.23f, axle * 0.73f);
                wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                wheel.transform.localScale = new Vector3(0.22f, 0.4f, 0.4f);
                wheel.GetComponent<Renderer>().sharedMaterial = tireMaterial;
                Destroy(wheel.GetComponent<Collider>());
            }

            CreateCarPart(car.transform, "Headlight", new Vector3(side * 0.43f, 0.62f, 1.19f), new Vector3(0.24f, 0.14f, 0.08f), lightMaterial);
            CreateCarPart(car.transform, "Tail light", new Vector3(side * 0.43f, 0.52f, -1.19f), new Vector3(0.22f, 0.14f, 0.08f), tailMaterial);
        }

        vehicle = car.AddComponent<DeliveryDashVehicle>();
        vehicle.Bind(this);
    }

    private void CreateCarPart(Transform parent, string partName, Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = partName;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = localScale;
        part.GetComponent<Renderer>().sharedMaterial = material;
        Collider partCollider = part.GetComponent<Collider>();
        if (partCollider != null)
        {
            Destroy(partCollider);
        }
    }

    private void SetupCamera()
    {
        followCamera = Camera.main;
        if (followCamera == null)
        {
            GameObject cameraObject = new GameObject("Delivery Dash Camera");
            cameraObject.tag = "MainCamera";
            followCamera = cameraObject.AddComponent<Camera>();
        }

        followCamera.fieldOfView = 56f;
        followCamera.nearClipPlane = 0.1f;
        followCamera.farClipPlane = 250f;
        followCamera.transform.position = playerStart + new Vector3(0f, 21f, -13f);
    }

    private void SetupLighting()
    {
        RenderSettings.ambientLight = new Color(0.62f, 0.68f, 0.74f);
        GameObject sunObject = new GameObject("Delivery Dash Sun");
        sunObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
        Light sun = sunObject.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.15f;
        sun.color = new Color(1f, 0.92f, 0.78f);
        sun.shadows = LightShadows.Soft;
    }

    private void BeginNextPickup()
    {
        int nextNode = deliveries == 0 ? 1 : Random.Range(0, MissionNodes.Length);
        if (nextNode == previousNodeIndex)
        {
            nextNode = (nextNode + 1) % MissionNodes.Length;
        }

        currentNodeIndex = nextNode;
        carryingParcel = false;
        statusMessage = "Find the gold parcel marker.";
        SetMissionMarker(MissionNodes[currentNodeIndex], pickupMaterial);
    }

    private void PickUpParcel()
    {
        carryingParcel = true;
        previousNodeIndex = currentNodeIndex;
        int destinationIndex = Random.Range(0, MissionNodes.Length);
        if (destinationIndex == currentNodeIndex)
        {
            destinationIndex = (destinationIndex + 1) % MissionNodes.Length;
        }

        currentNodeIndex = destinationIndex;
        statusMessage = "Parcel aboard. Head to the cyan drop-off.";
        SetMissionMarker(MissionNodes[currentNodeIndex], deliveryMaterial);
    }

    private void CompleteDelivery()
    {
        deliveries++;
        timeRemaining += DeliveryTimeBonus;
        previousNodeIndex = currentNodeIndex;

        if (deliveries >= DeliveryGoal)
        {
            FinishShift(true);
            return;
        }

        BeginNextPickup();
    }

    private void FinishShift(bool success)
    {
        gameOver = true;
        completedShift = success;
        statusMessage = success ? "All parcels delivered!" : "Shift over. Try another run.";
        if (vehicle != null)
        {
            vehicle.SetCanDrive(false);
        }
        if (targetMarker != null)
        {
            Destroy(targetMarker);
            targetMarker = null;
        }
    }

    private void SetMissionMarker(Vector3 position, Material markerMaterial)
    {
        if (targetMarker != null)
        {
            Destroy(targetMarker);
        }

        targetMarker = new GameObject(carryingParcel ? "Delivery Destination" : "Parcel Pickup");
        targetMarker.transform.position = position;
        CreateMarkerPart(targetMarker.transform, "Marker base", new Vector3(0f, 0.12f, 0f), new Vector3(1.8f, 0.14f, 1.8f), markerMaterial, PrimitiveType.Cylinder);
        CreateMarkerPart(targetMarker.transform, "Parcel icon", new Vector3(0f, 0.85f, 0f), new Vector3(0.66f, 0.66f, 0.66f), carryingParcel ? deliveryMaterial : parcelMaterial, PrimitiveType.Cube);
        CreateMarkerPart(targetMarker.transform, "Marker beacon", new Vector3(0f, 1.5f, 0f), new Vector3(0.28f, 0.28f, 0.28f), markerMaterial, PrimitiveType.Sphere);
    }

    private void CreateMarkerPart(Transform parent, string partName, Vector3 localPosition, Vector3 localScale, Material material, PrimitiveType primitiveType)
    {
        GameObject part = GameObject.CreatePrimitive(primitiveType);
        part.name = partName;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = localScale;
        part.GetComponent<Renderer>().sharedMaterial = material;
        Collider partCollider = part.GetComponent<Collider>();
        if (partCollider != null)
        {
            Destroy(partCollider);
        }
    }

    private string FormatTime(float seconds)
    {
        int totalSeconds = Mathf.CeilToInt(seconds);
        return (totalSeconds / 60).ToString("00") + ":" + (totalSeconds % 60).ToString("00");
    }
}
