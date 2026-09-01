using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 카드 프레임 Reveal 및 Hide 애니메이션에 필요한 에셋을 자동 생성하는 에디터 유틸리티.
/// Unity 메뉴: Tools > Card Frame Animator Setup
/// </summary>
public static class CardFrameAnimatorSetup
{
    private const string ControllerPath = "Assets/Animations/CardFrameController.controller";
    private const string RevealClipPath = "Assets/Animations/CardFrame_Reveal.anim";
    private const string HideClipPath = "Assets/Animations/CardFrame_Hide.anim";
    private const string IdleClipPath = "Assets/Animations/CardFrame_Idle.anim";
    private const string PrefabPath = "Assets/Resources/Prefabs/card.prefab";

    [MenuItem("Tools/Card Frame Animator Setup")]
    public static void Setup()
    {
        EnsureDirectory("Assets/Animations");

        // 스프라이트 시퀀스 로드
        Sprite[] sprites = Resources.LoadAll<Sprite>("Images/CardUseFrames");
        if (sprites == null || sprites.Length == 0)
        {
            Debug.LogError("[CardFrameAnimatorSetup] Resources/Images/CardUseFrames 경로에서 스프라이트를 로드할 수 없습니다.");
            return;
        }

        // 이름 기준으로 스프라이트 정렬
        System.Array.Sort(sprites, (left, right) => CompareSpritesByFrameName(left, right));

        // 기존 에셋 강제 정리 (덮어쓰기 보장)
        DeleteExistingAsset(ControllerPath);
        DeleteExistingAsset(RevealClipPath);
        DeleteExistingAsset(HideClipPath);
        DeleteExistingAsset(IdleClipPath);

        AnimationClip idleClip = CreateIdleClip();
        AnimationClip revealClip = CreateRevealClip(sprites);
        AnimationClip hideClip = CreateHideClip(sprites);
        AnimatorController controller = CreateController(idleClip, revealClip, hideClip);
        AttachToPrefab(controller);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[CardFrameAnimatorSetup] 카드 프레임 Animator 세팅이 완료되었습니다.\n"
            + $"  Sprites Loaded: {sprites.Length} frames\n"
            + $"  Controller: {ControllerPath}\n"
            + $"  Reveal Clip: {RevealClipPath}\n"
            + $"  Hide Clip: {HideClipPath}\n"
            + $"  Idle Clip: {IdleClipPath}");
    }

    private static AnimationClip CreateIdleClip()
    {
        AnimationClip clip = new AnimationClip { name = "CardFrame_Idle" };
        clip.frameRate = 24;

        AssetDatabase.CreateAsset(clip, IdleClipPath);
        return clip;
    }

    private static AnimationClip CreateRevealClip(Sprite[] sprites)
    {
        AnimationClip clip = new AnimationClip { name = "CardFrame_Reveal" };
        clip.frameRate = 24;

        // ── Sprite Keyframes (등장은 흩어졌던 테두리가 역순으로 조립되는 연출! frame_29 -> frame_00) ──
        ObjectReferenceKeyframe[] keys = new ObjectReferenceKeyframe[sprites.Length];
        float frameTime = 1f / 24f;
        for (int i = 0; i < sprites.Length; i++)
        {
            keys[i] = new ObjectReferenceKeyframe
            {
                time = i * frameTime,
                value = sprites[sprites.Length - 1 - i]
            };
        }

        EditorCurveBinding binding = new EditorCurveBinding
        {
            type = typeof(Image),
            path = "",
            propertyName = "m_Sprite"
        };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

        AssetDatabase.CreateAsset(clip, RevealClipPath);
        return clip;
    }

    private static AnimationClip CreateHideClip(Sprite[] sprites)
    {
        AnimationClip clip = new AnimationClip { name = "CardFrame_Hide" };
        clip.frameRate = 24;

        // ── Sprite Keyframes (원래 고유 등급 프레임을 보존하기 위해 0초 키프레임은 비워두고, 1프레임부터 frame_01을 매핑!) ──
        int keyCount = sprites.Length - 1;
        ObjectReferenceKeyframe[] keys = new ObjectReferenceKeyframe[keyCount];
        float frameTime = 1f / 24f;
        for (int i = 0; i < keyCount; i++)
        {
            keys[i] = new ObjectReferenceKeyframe
            {
                time = (i + 1) * frameTime,
                value = sprites[i + 1]
            };
        }

        EditorCurveBinding binding = new EditorCurveBinding
        {
            type = typeof(Image),
            path = "",
            propertyName = "m_Sprite"
        };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

        AssetDatabase.CreateAsset(clip, HideClipPath);
        return clip;
    }

    private static AnimatorController CreateController(AnimationClip idleClip, AnimationClip revealClip, AnimationClip hideClip)
    {
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        // 파라미터 추가
        controller.AddParameter("Reveal", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Hide", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine rootStateMachine = controller.layers[0].stateMachine;

        // 기본 Idle 상태
        AnimatorState idleState = rootStateMachine.AddState("Idle", new Vector3(250, 0, 0));
        idleState.motion = idleClip;
        rootStateMachine.defaultState = idleState;

        // Reveal 상태
        AnimatorState revealState = rootStateMachine.AddState("Reveal", new Vector3(500, 0, 0));
        revealState.motion = revealClip;

        // Hide 상태 (소멸)
        AnimatorState hideState = rootStateMachine.AddState("Hide", new Vector3(500, 100, 0));
        hideState.motion = hideClip;

        // Idle → Reveal 트랜지션
        AnimatorStateTransition toReveal = idleState.AddTransition(revealState);
        toReveal.AddCondition(AnimatorConditionMode.If, 0, "Reveal");
        toReveal.hasExitTime = false;
        toReveal.duration = 0f;

        // Reveal → Idle 트랜지션
        AnimatorStateTransition toIdle = revealState.AddTransition(idleState);
        toIdle.hasExitTime = true;
        toIdle.exitTime = 1f;
        toIdle.duration = 0f;

        // Any State → Hide 트랜지션 (어디서든 Hide 트리거 시 소멸 진행)
        AnimatorStateTransition toHide = rootStateMachine.AddAnyStateTransition(hideState);
        toHide.AddCondition(AnimatorConditionMode.If, 0, "Hide");
        toHide.hasExitTime = false;
        toHide.duration = 0f;
        toHide.canTransitionToSelf = false;

        // Hide → Idle 트랜지션
        AnimatorStateTransition hideToIdle = hideState.AddTransition(idleState);
        hideToIdle.hasExitTime = true;
        hideToIdle.exitTime = 1f;
        hideToIdle.duration = 0f;

        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void AttachToPrefab(AnimatorController controller)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"[CardFrameAnimatorSetup] 프리팹을 찾을 수 없습니다: {PrefabPath}\n"
                + "수동으로 CardFrameImage 오브젝트에 Animator를 추가하고 Controller를 등록해 주세요.");
            return;
        }

        HandCardUI handCardUI = prefab.GetComponent<HandCardUI>();
        if (handCardUI == null)
        {
            Debug.LogWarning("[CardFrameAnimatorSetup] 프리팹에서 HandCardUI 컴포넌트를 찾을 수 없습니다.");
            return;
        }

        Image frameImage = handCardUI.CardFrameImage;
        if (frameImage == null)
        {
            Debug.LogWarning("[CardFrameAnimatorSetup] HandCardUI.CardFrameImage가 null입니다.");
            return;
        }

        Animator animator = frameImage.GetComponent<Animator>();
        if (animator == null)
            animator = frameImage.gameObject.AddComponent<Animator>();

        animator.runtimeAnimatorController = controller;

        EditorUtility.SetDirty(prefab);
        Debug.Log("[CardFrameAnimatorSetup] 카드 프리팹의 CardFrameImage에 Animator가 연결되었습니다.");
    }

    private static void EnsureDirectory(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string folder = System.IO.Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, folder);
        }
    }

    private static void DeleteExistingAsset(string path)
    {
        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null)
            AssetDatabase.DeleteAsset(path);
    }

    private static int CompareSpritesByFrameName(Sprite left, Sprite right) =>
        CompareFrameNames(left != null ? left.name : string.Empty, right != null ? right.name : string.Empty);

    private static int CompareFrameNames(string left, string right)
    {
        int leftNumber = ExtractTrailingNumber(left);
        int rightNumber = ExtractTrailingNumber(right);
        if (leftNumber != rightNumber)
            return leftNumber.CompareTo(rightNumber);

        return string.CompareOrdinal(left, right);
    }

    private static int ExtractTrailingNumber(string name)
    {
        if (string.IsNullOrEmpty(name))
            return int.MinValue;

        int index = name.Length - 1;
        while (index >= 0 && char.IsDigit(name[index]))
            index--;

        if (index == name.Length - 1)
            return int.MinValue;

        string digits = name.Substring(index + 1);
        return int.TryParse(digits, out int value) ? value : int.MinValue;
    }
}
