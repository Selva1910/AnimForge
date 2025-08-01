using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public class AnimForgeWindow : EditorWindow
{
    private AnimationClip animationClip;
    private GameObject modelPrefab;
    private AnimationCurve speedCurve = AnimationCurve.Linear(0, 1, 1, 1);

    private float time = 0f;
    private double lastTime;
    private bool isPlaying = false;

    private GameObject prefabInstanceRoot;


    private float trimStartTime = 0f;
    private float trimEndTime = 1f;
    private bool isDraggingTrimStart = false;
    private bool isDraggingTrimEnd = false;
    private bool isTrimPlaying = false;


    [MenuItem("Anim Forge/Open Editor")]
    public static void ShowWindow()
    {
        var wind  = GetWindow<AnimForgeWindow>("Anim Forge");
        wind.minSize = new Vector2(500,500);

    }

    private void OnEnable()
    {
        EditorApplication.update += OnEditorUpdate;
        lastTime = EditorApplication.timeSinceStartup;
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
        AnimationMode.StopAnimationMode();
    }

    private string[] tabs = new string[] { "Trim", "Time Remap", "Retarget" };
    private int selectedTab = 0;

    private void OnGUI()
    {
        DrawSetup();

        DrawToolbar();

        GUILayout.Space(10);

        switch (selectedTab)
        {
            case 0:
                DrawEditorTool();
                break;
            case 1:
                break;
            case 2:
                break;
        }


    }
    private void DrawToolbar()
    {
        selectedTab = GUILayout.Toolbar(selectedTab, tabs);
    }

    private void DrawEditorTool()
    {

        if (prefabInstanceRoot != null)
        {
            GUILayout.Space(10);
            DrawPlaybackControls();
        }

        GUILayout.Space(10);

        if (animationClip != null && GUILayout.Button("Bake New Clip with Speed Curve"))
        {
            var baked = AnimationClipBaker.BakeClipWithSpeedCurve(animationClip, speedCurve);
            AnimationClipBaker.SaveClipAsAsset(baked);
        }

        GUILayout.Space(10);

        if (animationClip != null)
        {
            GUILayout.Label("Trim Clip", EditorStyles.boldLabel);
            
            trimStartTime = EditorGUILayout.FloatField("Trim Start (s)", trimStartTime);
            trimEndTime = EditorGUILayout.FloatField("Trim End (s)", trimEndTime);

            if (GUILayout.Button("Trim Clip and Save"))
            {
                var trimmed = AnimationClipBaker.TrimClip(animationClip, trimStartTime, trimEndTime);
                AnimationClipBaker.SaveClipAsAsset(trimmed, "TrimmedClip");
            }
        }
    }

    private void DrawSetup()
    {
        modelPrefab = (GameObject)EditorGUILayout.ObjectField("Model Prefab", modelPrefab, typeof(GameObject), false);
        animationClip = (AnimationClip)EditorGUILayout.ObjectField("Animation Clip", animationClip, typeof(AnimationClip), false);

        speedCurve = EditorGUILayout.CurveField("Speed Curve", speedCurve);

        GUILayout.Space(10);

        if (GUILayout.Button("Open Prefab In Edit Mode"))
        {
            OpenPrefabAndPreparePreview();
        }
    }

    private void DrawPlaybackControls()
    {
        GUILayout.BeginHorizontal();

        if (GUILayout.Button(isPlaying ? "Pause" : "Play"))
        {
            isPlaying = !isPlaying;
            isTrimPlaying = false;

            if (isPlaying)
            {
                AnimationMode.StartAnimationMode();
            }
        }

        if (GUILayout.Button("Trim Play"))
        {
            isTrimPlaying = !isTrimPlaying;
            isPlaying = false;

            if (isTrimPlaying)
            {
                AnimationMode.StartAnimationMode();
                time = Mathf.Clamp(time, trimStartTime, trimEndTime);
            }
        }

        if (GUILayout.Button("Stop"))
        {
            isPlaying = false;
            isTrimPlaying = false;
            time = 0f;
            AnimationMode.SampleAnimationClip(prefabInstanceRoot, animationClip, 0f);
        }

        GUILayout.EndHorizontal();

        Rect timelineRect = GUILayoutUtility.GetRect(10, 40, GUILayout.Width(Screen.width - 20));
        timelineRect.position = new Vector2(timelineRect.x + 10, timelineRect.y);
        EditorGUI.DrawRect(timelineRect, new Color(0.15f, 0.15f, 0.15f));

        float startX = Mathf.Lerp(timelineRect.x, timelineRect.xMax, trimStartTime / animationClip.length);
        float endX = Mathf.Lerp(timelineRect.x, timelineRect.xMax, trimEndTime / animationClip.length);
        float handlePos = Mathf.Lerp(timelineRect.x, timelineRect.xMax, time / animationClip.length);

        // Draw scrub time indicator
        EditorGUI.DrawRect(new Rect(handlePos - 1, timelineRect.y, 2, timelineRect.height), Color.red);

        // Draw trim handles
        EditorGUI.DrawRect(new Rect(startX - 2, timelineRect.y, 4, timelineRect.height), Color.cyan);
        EditorGUI.DrawRect(new Rect(endX - 2, timelineRect.y, 4, timelineRect.height), Color.magenta);

        // Interaction
        Event e = Event.current;

        if (e.type == EventType.MouseDown && timelineRect.Contains(e.mousePosition))
        {
            if (Mathf.Abs(e.mousePosition.x - startX) < 6)
                isDraggingTrimStart = true;
            else if (Mathf.Abs(e.mousePosition.x - endX) < 6)
                isDraggingTrimEnd = true;
        }

        if (e.type == EventType.MouseUp)
        {
            isDraggingTrimStart = false;
            isDraggingTrimEnd = false;
        }

        if (e.type == EventType.MouseDrag && timelineRect.Contains(e.mousePosition))
        {
            float normalized = Mathf.InverseLerp(timelineRect.x, timelineRect.xMax, e.mousePosition.x);
            float new1Time = Mathf.Clamp(normalized * animationClip.length, 0f, animationClip.length);

            if (isDraggingTrimStart)
            {
                trimStartTime = Mathf.Clamp(new1Time, 0f, trimEndTime - 0.01f);
                e.Use();
                Repaint();
            }
            else if (isDraggingTrimEnd)
            {
                trimEndTime = Mathf.Clamp(new1Time, trimStartTime + 0.01f, animationClip.length);
                e.Use();
                Repaint();
            }
        }

        // Scrub time
        EditorGUI.BeginChangeCheck();
        float newTime = GUI.HorizontalSlider(timelineRect, time, 0f, animationClip.length);
        if (EditorGUI.EndChangeCheck())
        {
            time = newTime;
            AnimationMode.SampleAnimationClip(prefabInstanceRoot, animationClip, time);
            Repaint();
        }

        GUILayout.Label($"Time: {time:F2} / {animationClip.length:F2} sec");
        GUILayout.Label($"Trim Range: {trimStartTime:F2}s → {trimEndTime:F2}s");
    }

    private void OnEditorUpdate()
    {
        if ((!isPlaying && !isTrimPlaying) || animationClip == null || prefabInstanceRoot == null)
            return;

        double now = EditorApplication.timeSinceStartup;
        float deltaTime = (float)(now - lastTime);
        lastTime = now;

        float speed = speedCurve.Evaluate(time);
        speed = Mathf.Max(speed, 0.001f);
        time += deltaTime * speed;

        if (isTrimPlaying)
        {
            if (time > trimEndTime)
                time = trimStartTime;
        }
        else
        {
            if (time > animationClip.length)
                time = 0f;
        }

        AnimationMode.SampleAnimationClip(prefabInstanceRoot, animationClip, time);
        SceneView.RepaintAll();
        Repaint();
    }


    private void OpenPrefabAndPreparePreview()
    {
        if (modelPrefab == null)
        {
            Debug.LogWarning("Model prefab not assigned.");
            return;
        }

        string path = AssetDatabase.GetAssetPath(modelPrefab);
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogError("Invalid prefab path.");
            return;
        }

        var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
        if (prefabStage == null || prefabStage.assetPath != path)
        {
            AssetDatabase.OpenAsset(modelPrefab);
            EditorApplication.delayCall += () =>
            {
                SetupPrefabRootAndController();
            };
        }
        else
        {
            SetupPrefabRootAndController();
        }

        time = 0f;
    }
    private void SetupPrefabRootAndController()
    {
        var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
        if (prefabStage == null)
        {
            Debug.LogError("Prefab Stage not open.");
            return;
        }

        prefabInstanceRoot = prefabStage.prefabContentsRoot;

        if (prefabInstanceRoot == null)
        {
            Debug.LogError("Prefab root not found in prefab stage.");
            return;
        }

        var animator = prefabInstanceRoot.GetComponent<Animator>();
        if (animator == null)
            animator = prefabInstanceRoot.AddComponent<Animator>();

        if (animationClip != null)
        {
            string tempPath = "Assets/__Temp__Preview.controller";

            // Prevent creating multiple copies
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(tempPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPathWithClip(tempPath, animationClip);
            }
            else
            {
                controller.layers[0].stateMachine.states = new ChildAnimatorState[0];
                var state = controller.layers[0].stateMachine.AddState("Preview");
                state.motion = animationClip;
                controller.layers[0].stateMachine.defaultState = state;
            }

            animator.runtimeAnimatorController = controller;
        }

        AnimationMode.StartAnimationMode();
    }


}
