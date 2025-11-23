using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;
using System.Linq;
using Gallop;

//初期ポーズ(T,Aポーズ)の時点でアタッチ、有効化されている必要がある
public class UnityHumanoidVMDRecorder : MonoBehaviour
{
    public const string FileSavePath = "/../VMDRecords";
    public bool UseParentOfAll = true;
    public bool UseCenterAsParentOfAll = true;
    /// <summary>
    /// 全ての親の座標・回転を絶対座標系で計算する
    /// UseParentOfAllがTrueでないと意味がない
    /// </summary>
    public bool UseAbsoluteCoordinateSystem = false;
    public bool IgnoreInitialPosition = false;
    public bool IgnoreInitialRotation = false;
    /// <summary>
    /// 一部のモデルではMMD上ではセンターが足元にある
    /// Start前に設定されている必要がある
    /// </summary>
    public bool UseBottomCenter = false;
    /// <summary>
    /// Unity上のモーフ名に1.まばたきなど番号が振られている場合、番号を除去する
    /// </summary>
    public bool TrimMorphNumber = false;
    public int KeyReductionLevel = 2;
    public bool IsRecording { get; private set; } = false;
    public int FrameNumber { get; private set; } = 0;
    int frameNumberSaved = 0;
    const float FPSs = 0.03333f;
    const string CenterNameString = "Position";

    public enum BoneNames
    {
        Position,
        Hip, Waist, Spine, Chest, Head, Neck,
        Thigh_L, Ankle_L, Knee_L, Toe_L, Thigh_R, Ankle_R, Knee_R, Toe_R,
        //Offset Are very Important For Legs or else Character Foot Fucked up lol
        Ankle_offset_L, Toe_offset_L, Ankle_offset_R, Toe_offset_R,
        Shoulder_L, Arm_L, Elbow_L, Wrist_L, Shoulder_R, Arm_R, Elbow_R, Wrist_R,
        Thumb_01_L, Thumb_02_L, Thumb_03_L, Thumb_01_R, Thumb_02_R, Thumb_03_R,
        Index_01_L, Index_02_L, Index_03_L, Index_01_R, Index_02_R, Index_03_R,
        Middle_01_L, Middle_02_L, Middle_03_L, Middle_01_R, Middle_02_R, Middle_03_R,
        Ring_01_L, Ring_02_L, Ring_03_L, Ring_01_R, Ring_02_R, Ring_03_R,
        Pinky_01_L, Pinky_02_L, Pinky_03_L, Pinky_01_R, Pinky_02_R, Pinky_03_R,
        Tail_Ctrl,
        None
    }
    //コンストラクタにて初期化
    //全てのボーンを名前で引く辞書
    Dictionary<string, Transform> transformDictionary = new Dictionary<string, Transform>();
    public Dictionary<BoneNames, Transform> BoneDictionary { get; private set; }
    Vector3 parentInitialPosition = Vector3.zero;
    Quaternion parentInitialRotation = Quaternion.identity;
    Dictionary<BoneNames, List<Vector3>> positionDictionary = new Dictionary<BoneNames, List<Vector3>>();
    Dictionary<BoneNames, List<Vector3>> positionDictionarySaved = new Dictionary<BoneNames, List<Vector3>>();
    Dictionary<BoneNames, List<Quaternion>> rotationDictionary = new Dictionary<BoneNames, List<Quaternion>>();
    Dictionary<BoneNames, List<Quaternion>> rotationDictionarySaved = new Dictionary<BoneNames, List<Quaternion>>();
    Dictionary<int, bool> visitableDictionary = new Dictionary<int, bool>();
    //ボーン移動量の補正係数
    //この値は大体の値、正確ではない
    const float DefaultBoneAmplifier = 12.5f;

    public Vector3 ParentOfAllOffset = new Vector3(0, 0, 0);
    public Vector3 LeftFootIKOffset = Vector3.zero;
    public Vector3 RightFootIKOffset = Vector3.zero;

    BoneGhost boneGhost;
    
    private UmaContainer container;
    float aposeDegress = 38.5f;

    public bool IsLive;

    bool IsMini;
    public void Initialize()
    {
        Time.fixedDeltaTime = FPSs;
        container = GetComponentInParent<UmaContainer>();
        List<Transform> objs = GetComponentsInChildren<Transform>().ToList();
        var characterContainer = GetComponentInParent<UmaContainerCharacter>();

        IsMini = characterContainer.IsMini;
        
        BoneDictionary = new Dictionary<BoneNames, Transform>()
        {
            { BoneNames.Position, objs.Find(a=>a.name.Equals("Position"))},
            { BoneNames.Hip, objs.Find(a=>a.name.Equals("Hip"))},
            { BoneNames.Waist,   objs.Find(a=>a.name.Equals("Waist"))},
            { BoneNames.Spine,   objs.Find(a=>a.name.Equals("Spine"))},
            { BoneNames.Chest,  objs.Find(a=>a.name.Equals("Chest"))},

            { BoneNames.Head,       objs.Find(a=>a.name.Equals("Head"))},
            { BoneNames.Neck,       objs.Find(a=>a.name.Equals("Neck"))},

            { BoneNames.Tail_Ctrl, objs.Find(a=>a.name.Equals("Tail_Ctrl"))},

            { BoneNames.Thigh_L,     objs.Find(a=>a.name.Equals("Thigh_L"))},
            { BoneNames.Knee_L,   objs.Find(a=>a.name.Equals("Knee_L"))},
            { BoneNames.Ankle_L,     objs.Find(a=>a.name.Equals("Ankle_L"))},
            { BoneNames.Ankle_offset_L,   objs.Find(a=>a.name.Equals("Ankle_offset_L"))},
            { BoneNames.Toe_L,   objs.Find(a=>a.name.Equals("Toe_L"))},
            { BoneNames.Toe_offset_L,   objs.Find(a=>a.name.Equals("Toe_offset_L"))},

            { BoneNames.Thigh_R,     objs.Find(a=>a.name.Equals("Thigh_R"))},
            { BoneNames.Knee_R,   objs.Find(a=>a.name.Equals("Knee_R"))},
            { BoneNames.Ankle_R,     objs.Find(a=>a.name.Equals("Ankle_R"))},
            { BoneNames.Ankle_offset_R,   objs.Find(a=>a.name.Equals("Ankle_offset_R"))},
            { BoneNames.Toe_R,   objs.Find(a=>a.name.Equals("Toe_R"))},
            { BoneNames.Toe_offset_R,   objs.Find(a=>a.name.Equals("Toe_offset_R"))},

            { BoneNames.Shoulder_L,     objs.Find(a=>a.name.Equals("Shoulder_L"))},
            { BoneNames.Arm_L,     objs.Find(a=>a.name.Equals("Arm_L"))},
            { BoneNames.Elbow_L,   objs.Find(a=>a.name.Equals("Elbow_L"))},
            { BoneNames.Wrist_L,   objs.Find(a=>a.name.Equals("Wrist_L"))},

            { BoneNames.Shoulder_R,     objs.Find(a=>a.name.Equals("Shoulder_R"))},
            { BoneNames.Arm_R,     objs.Find(a=>a.name.Equals("Arm_R"))},
            { BoneNames.Elbow_R,   objs.Find(a=>a.name.Equals("Elbow_R"))},
            { BoneNames.Wrist_R,   objs.Find(a=>a.name.Equals("Wrist_R"))},

            { BoneNames.Thumb_01_L, objs.Find(a=>a.name.Equals("Thumb_01_L"))},
            { BoneNames.Thumb_02_L, objs.Find(a=>a.name.Equals("Thumb_02_L"))},
            { BoneNames.Thumb_03_L, objs.Find(a=>a.name.Equals("Thumb_03_L"))},

            { BoneNames.Index_01_L, objs.Find(a=>a.name.Equals("Index_01_L"))},
            { BoneNames.Index_02_L, objs.Find(a=>a.name.Equals("Index_02_L"))},
            { BoneNames.Index_03_L, objs.Find(a=>a.name.Equals("Index_03_L"))},

            { BoneNames.Middle_01_L, objs.Find(a=>a.name.Equals("Middle_01_L"))},
            { BoneNames.Middle_02_L, objs.Find(a=>a.name.Equals("Middle_02_L"))},
            { BoneNames.Middle_03_L, objs.Find(a=>a.name.Equals("Middle_03_L"))},

            { BoneNames.Ring_01_L, objs.Find(a=>a.name.Equals("Ring_01_L"))},
            { BoneNames.Ring_02_L, objs.Find(a=>a.name.Equals("Ring_02_L"))},
            { BoneNames.Ring_03_L, objs.Find(a=>a.name.Equals("Ring_03_L"))},

            { BoneNames.Pinky_01_L, objs.Find(a=>a.name.Equals("Pinky_01_L"))},
            { BoneNames.Pinky_02_L, objs.Find(a=>a.name.Equals("Pinky_02_L"))},
            { BoneNames.Pinky_03_L, objs.Find(a=>a.name.Equals("Pinky_03_L"))},

            { BoneNames.Thumb_01_R, objs.Find(a=>a.name.Equals("Thumb_01_R"))},
            { BoneNames.Thumb_02_R, objs.Find(a=>a.name.Equals("Thumb_02_R"))},
            { BoneNames.Thumb_03_R, objs.Find(a=>a.name.Equals("Thumb_03_R"))},

            { BoneNames.Index_01_R, objs.Find(a=>a.name.Equals("Index_01_R"))},
            { BoneNames.Index_02_R, objs.Find(a=>a.name.Equals("Index_02_R"))},
            { BoneNames.Index_03_R, objs.Find(a=>a.name.Equals("Index_03_R"))},

            { BoneNames.Middle_01_R, objs.Find(a=>a.name.Equals("Middle_01_R"))},
            { BoneNames.Middle_02_R, objs.Find(a=>a.name.Equals("Middle_02_R"))},
            { BoneNames.Middle_03_R, objs.Find(a=>a.name.Equals("Middle_03_R"))},

            { BoneNames.Ring_01_R, objs.Find(a=>a.name.Equals("Ring_01_R"))},
            { BoneNames.Ring_02_R, objs.Find(a=>a.name.Equals("Ring_02_R"))},
            { BoneNames.Ring_03_R, objs.Find(a=>a.name.Equals("Ring_03_R"))},

            { BoneNames.Pinky_01_R, objs.Find(a=>a.name.Equals("Pinky_01_R"))},
            { BoneNames.Pinky_02_R, objs.Find(a=>a.name.Equals("Pinky_02_R"))},
            { BoneNames.Pinky_03_R, objs.Find(a=>a.name.Equals("Pinky_03_R"))},  
        };
        

        foreach (KeyValuePair<BoneNames, Transform> pair in BoneDictionary)
        {
            transformDictionary.Add(pair.Key.ToString(), pair.Value);
        }

        
        var animator = characterContainer.UmaAnimator;
        var state = animator.GetCurrentAnimatorStateInfo(0);
        animator.enabled = false;

        // Set to T-Pose
        characterContainer.ResetBodyPose();
        characterContainer.UpBodyReset();

        SetInitialPositionAndRotation();

        foreach (BoneNames boneName in BoneDictionary.Keys)
        {
            if (BoneDictionary[boneName] == null) { continue; }

            positionDictionary.Add(boneName, new List<Vector3>());
            rotationDictionary.Add(boneName, new List<Quaternion>());
        }

        boneGhost = new BoneGhost(BoneDictionary, UseBottomCenter, IsMini);
        animator.enabled = true;
        animator.Play(state.shortNameHash, 0, state.normalizedTime);
    }

    private void FixedUpdate()
    {
        if (IsRecording && !IsLive)
        {
            SaveFrame();
            FrameNumber++;
        }
    }

    bool lastvisable;
    void SaveFrame()
    {
        if (boneGhost != null) { boneGhost.GhostAll(); }

        bool visable = container.LiveVisible;
        if (visitableDictionary.Count == 0)
        {
            lastvisable = visable;
            visitableDictionary.Add(0, visable);
        }
        else if(visable != lastvisable)
        {
            lastvisable = visable;
            visitableDictionary.Add(FrameNumber, visable);
        }

        foreach (BoneNames boneName in BoneDictionary.Keys)
        {
            if (BoneDictionary[boneName] == null)
            {
                continue;
            }

            if (boneGhost != null && boneGhost.GhostDictionary.Keys.Contains(boneName))
            {
                if (boneGhost.GhostDictionary[boneName].ghost == null || !boneGhost.GhostDictionary[boneName].enabled)
                {
                    rotationDictionary[boneName].Add(Quaternion.identity);
                    positionDictionary[boneName].Add(Vector3.zero);
                    continue;
                }

                Vector3 boneVector = boneGhost.GhostDictionary[boneName].ghost.localPosition;
                Quaternion boneQuatenion = boneGhost.GhostDictionary[boneName].ghost.localRotation;
                rotationDictionary[boneName].Add(new Quaternion(-boneQuatenion.x, boneQuatenion.y, -boneQuatenion.z, boneQuatenion.w));

                boneVector -= boneGhost.GhostOriginalLocalPositionDictionary[boneName];

                positionDictionary[boneName].Add(new Vector3(-boneVector.x, boneVector.y, -boneVector.z) * DefaultBoneAmplifier);
                continue;
            }

            Quaternion fixedQuatenion = Quaternion.identity;
            Quaternion vmdRotation = Quaternion.identity;

            if (boneName == BoneNames.Position && UseAbsoluteCoordinateSystem)
            {
                fixedQuatenion = BoneDictionary[boneName].rotation;
            }
            else
            {
                fixedQuatenion = BoneDictionary[boneName].localRotation;
            }

            if (boneName == BoneNames.Position && IgnoreInitialRotation)
            {
                fixedQuatenion = BoneDictionary[boneName].localRotation.MinusRotation(parentInitialRotation);
            }

            vmdRotation = new Quaternion(-fixedQuatenion.x, fixedQuatenion.y, -fixedQuatenion.z, fixedQuatenion.w);

            rotationDictionary[boneName].Add(vmdRotation);

            Vector3 fixedPosition = Vector3.zero;
            Vector3 vmdPosition = Vector3.zero;

            if (boneName == BoneNames.Position && UseAbsoluteCoordinateSystem)
            {
                fixedPosition = BoneDictionary[boneName].position;
            }
            else
            {
                fixedPosition = BoneDictionary[boneName].localPosition;
            }

            if (boneName == BoneNames.Position && IgnoreInitialPosition)
            {
                fixedPosition -= parentInitialPosition;
            }

            vmdPosition = new Vector3(-fixedPosition.x, fixedPosition.y, -fixedPosition.z);

            if (boneName == BoneNames.Position)
            {
                positionDictionary[boneName].Add(vmdPosition * DefaultBoneAmplifier + ParentOfAllOffset);
            }
            else
            {
                positionDictionary[boneName].Add(vmdPosition * DefaultBoneAmplifier);
            }
        }
    }

    void LiveSaveFrame()
    {
        if (IsRecording && IsLive)
        {
            SaveFrame();
            FrameNumber++;
        }
    }

    void SetInitialPositionAndRotation()
    {
        if (UseAbsoluteCoordinateSystem)
        {
            parentInitialPosition = transform.position;
            parentInitialRotation = transform.rotation;
        }
        else
        {
            parentInitialPosition = transform.localPosition;
            parentInitialRotation = transform.localRotation;
        }
    }

    public static void SetFPS(int fps)
    {
        Time.fixedDeltaTime = 1 / (float)fps;
    }

    /// <summary>
    /// レコーディングを開始または再開
    /// </summary>
    public void StartRecording(bool islive = false)
    {
        SetInitialPositionAndRotation();
        IsRecording = true;
        IsLive = islive;

        if (islive)
        {
            var director = Gallop.Live.Director.instance;
            director._liveTimelineControl.RecordUma += LiveSaveFrame;
        }
    }

    /// <summary>
    /// レコーディングを一時停止
    /// </summary>
    public void PauseRecording() { IsRecording = false; }

    /// <summary>
    /// レコーディングを終了
    /// </summary>
    public void StopRecording()
    {
        IsRecording = false;
        frameNumberSaved = FrameNumber;
        FrameNumber = 0;
        positionDictionarySaved = positionDictionary;
        positionDictionary = new Dictionary<BoneNames, List<Vector3>>();
        rotationDictionarySaved = rotationDictionary;
        rotationDictionary = new Dictionary<BoneNames, List<Quaternion>>();
        foreach (BoneNames boneName in BoneDictionary.Keys)
        {
            if (BoneDictionary[boneName] == null) { continue; }

            positionDictionary.Add(boneName, new List<Vector3>());
            rotationDictionary.Add(boneName, new List<Quaternion>());
        }

        if (IsLive)
        {
            var director = Gallop.Live.Director.instance;
            director._liveTimelineControl.RecordUma -= LiveSaveFrame;
        }
    }

    /// <summary>
    /// VMDを作成する
    /// 呼び出す際は先にStopRecordingを呼び出すこと
    /// </summary>
    /// <param name="modelName">VMDファイルに記載される専用モデル名</param>
    /// <param name="filePath">保存先の絶対ファイルパス</param>
    public void SaveVMD(string modelName, string filePath)
    {
        if (IsRecording)
        {
            Debug.Log(transform.name + "VMD保存前にレコーディングをストップしてください。");
            return;
        }

        if (KeyReductionLevel <= 0) { KeyReductionLevel = 1; }

        Debug.Log(transform.name + "VMDファイル作成開始");
        //ファイルの書き込み
        using (FileStream fileStream = new FileStream(filePath, FileMode.Create))
        using (BinaryWriter binaryWriter = new BinaryWriter(fileStream))
        {
            try
            {
                const string ShiftJIS = "shift_jis";
                const int intByteLength = 4;

                //ファイルタイプの書き込み
                const int fileTypeLength = 30;
                const string RightFileType = "Vocaloid Motion Data 0002";
                byte[] fileTypeBytes = System.Text.Encoding.GetEncoding(ShiftJIS).GetBytes(RightFileType);
                binaryWriter.Write(fileTypeBytes, 0, fileTypeBytes.Length);
                binaryWriter.Write(new byte[fileTypeLength - fileTypeBytes.Length], 0, fileTypeLength - fileTypeBytes.Length);

                //モデル名の書き込み、Shift_JISで保存
                const int modelNameLength = 20;
                byte[] modelNameBytes = System.Text.Encoding.GetEncoding(ShiftJIS).GetBytes(modelName);
                //モデル名が長すぎたとき
                modelNameBytes = modelNameBytes.Take(Mathf.Min(modelNameLength, modelNameBytes.Length)).ToArray();
                binaryWriter.Write(modelNameBytes, 0, modelNameBytes.Length);
                binaryWriter.Write(new byte[modelNameLength - modelNameBytes.Length], 0, modelNameLength - modelNameBytes.Length);

                //全ボーンフレーム数の書き込み
                void LoopWithBoneCondition(Action<BoneNames, int> action)
                {
                    for (int i = 0; i < frameNumberSaved; i++)
                    {
                        foreach (BoneNames boneName in Enum.GetValues(typeof(BoneNames)))
                        {
                            if ((i % KeyReductionLevel) != 0 && boneName != BoneNames.Position) { continue; }
                            if (!BoneDictionary.Keys.Contains(boneName)) { continue; }
                            if (BoneDictionary[boneName] == null) { continue; }
                            if (!UseParentOfAll && boneName == BoneNames.Position) { continue; }

                            action(boneName, i);
                        }
                    }
                }
                uint allKeyFrameNumber = 0;
                LoopWithBoneCondition((a, b) => { allKeyFrameNumber++; });
                byte[] allKeyFrameNumberByte = BitConverter.GetBytes(allKeyFrameNumber);
                binaryWriter.Write(allKeyFrameNumberByte, 0, intByteLength);

                //人ボーンの書き込み
                LoopWithBoneCondition((boneName, i) =>
                {
                    const int boneNameLength = 15;
                    string boneNameString = boneName.ToString();
                    byte[] boneNameBytes = System.Text.Encoding.GetEncoding(ShiftJIS).GetBytes(boneNameString);
                    binaryWriter.Write(boneNameBytes, 0, boneNameBytes.Length);
                    binaryWriter.Write(new byte[boneNameLength - boneNameBytes.Length], 0, boneNameLength - boneNameBytes.Length);

                    byte[] frameNumberByte = BitConverter.GetBytes((ulong)i);
                    binaryWriter.Write(frameNumberByte, 0, intByteLength);

                    Vector3 position = positionDictionarySaved[boneName][i];
                    byte[] positionX = BitConverter.GetBytes(position.x);
                    binaryWriter.Write(positionX, 0, intByteLength);
                    byte[] positionY = BitConverter.GetBytes(position.y);
                    binaryWriter.Write(positionY, 0, intByteLength);
                    byte[] positionZ = BitConverter.GetBytes(position.z);
                    binaryWriter.Write(positionZ, 0, intByteLength);
                    Quaternion rotation = rotationDictionarySaved[boneName][i];
                    byte[] rotationX = BitConverter.GetBytes(rotation.x);
                    binaryWriter.Write(rotationX, 0, intByteLength);
                    byte[] rotationY = BitConverter.GetBytes(rotation.y);
                    binaryWriter.Write(rotationY, 0, intByteLength);
                    byte[] rotationZ = BitConverter.GetBytes(rotation.z);
                    binaryWriter.Write(rotationZ, 0, intByteLength);
                    byte[] rotationW = BitConverter.GetBytes(rotation.w);
                    binaryWriter.Write(rotationW, 0, intByteLength);

                    byte[] interpolateBytes = new byte[64];
                    binaryWriter.Write(interpolateBytes, 0, 64);
                });

                //カメラの書き込み
                byte[] cameraFrameCount = BitConverter.GetBytes(0);
                binaryWriter.Write(cameraFrameCount, 0, intByteLength);

                //照明の書き込み
                byte[] lightFrameCount = BitConverter.GetBytes(0);
                binaryWriter.Write(lightFrameCount, 0, intByteLength);

                //セルフシャドウの書き込み
                byte[] selfShadowCount = BitConverter.GetBytes(0);
                binaryWriter.Write(selfShadowCount, 0, intByteLength);
            }
            catch (Exception ex)
            {
                Debug.Log("VMD書き込みエラー" + ex.Message);
            }
            finally
            {
                binaryWriter.Close();
            }
        }

        if (boneGhost != null)
        {
            foreach(var pair in boneGhost.GhostDictionary)
            {
                if (pair.Value.ghost != null)
                {
                    Destroy(pair.Value.ghost.gameObject);
                }
            }
        }
        Destroy(this);
    }

    /// <summary>
    /// VMDを作成する
    /// 呼び出す際は先にStopRecordingを呼び出すこと
    /// </summary>
    /// <param name="modelName">VMDファイルに記載される専用モデル名</param>
    /// <param name="filePath">保存先の絶対ファイルパス</param>
    /// <param name="keyReductionLevel">キーの書き込み頻度を減らして容量を減らす</param>
    public void SaveVMD(string modelName, int keyReductionLevel = 3)
    {
        string fileName = $"{Application.dataPath}{FileSavePath}/{string.Format("UMA_{0}.vmd", DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"))}";
        Directory.CreateDirectory(Application.dataPath + FileSavePath);
        KeyReductionLevel = keyReductionLevel;
        SaveVMD(modelName, fileName);
    }

    public void SaveLiveVMD(LiveEntry liveEntry, DateTime time ,string modelName, int keyReductionLevel = 3)
    {
        string fileName = $"{Application.dataPath}{FileSavePath}/Live{liveEntry.MusicId}_{time.ToString("yyyy-MM-dd_HH-mm-ss")}/{modelName}.vmd";
        Directory.CreateDirectory(Path.GetDirectoryName(fileName));
        KeyReductionLevel = keyReductionLevel;
        SaveVMD(modelName, fileName);
    }

    //裏で正規化されたモデル
    //(初期ポーズで各ボーンのlocalRotationがQuaternion.identityのモデル)を疑似的にアニメーションさせる
    class BoneGhost
    {
        public Dictionary<BoneNames, (Transform ghost, bool enabled)> GhostDictionary { get; private set; } = new Dictionary<BoneNames, (Transform ghost, bool enabled)>();
        public Dictionary<BoneNames, Vector3> GhostOriginalLocalPositionDictionary { get; private set; } = new Dictionary<BoneNames, Vector3>();
        public Dictionary<BoneNames, Quaternion> GhostOriginalRotationDictionary { get; private set; } = new Dictionary<BoneNames, Quaternion>();
        public Dictionary<BoneNames, Quaternion> OriginalRotationDictionary { get; private set; } = new Dictionary<BoneNames, Quaternion>();

        public bool UseBottomCenter { get; private set; } = false;

        const string GhostSalt = "Ghost";
        private Dictionary<BoneNames, Transform> boneDictionary = new Dictionary<BoneNames, Transform>();
        float centerOffsetLength = 0;

        Dictionary<BoneNames, (BoneNames optionParent1, BoneNames optionParent2, BoneNames necessaryParent)> boneParentDictionary;

        public BoneGhost(Dictionary<BoneNames, Transform> boneDictionary, bool useBottomCenter, bool IsMini)
        {
            this.boneDictionary = boneDictionary;
            UseBottomCenter = useBottomCenter;

            if(IsMini)
            {
                boneParentDictionary = new Dictionary<BoneNames, (BoneNames optionParent1, BoneNames optionParent2, BoneNames necessaryParent)>()
                {
                    { BoneNames.Hip, (BoneNames.None, BoneNames.None, BoneNames.Position) },
                    { BoneNames.Waist,   (BoneNames.None, BoneNames.None, BoneNames.Hip) },
                    { BoneNames.Chest,  (BoneNames.None, BoneNames.None, BoneNames.Waist) },

                    { BoneNames.Tail_Ctrl,   (BoneNames.None, BoneNames.None, BoneNames.Hip) },
                    { BoneNames.Neck,       (BoneNames.Chest, BoneNames.None, BoneNames.Waist) },
                    { BoneNames.Head,       (BoneNames.Neck, BoneNames.Chest, BoneNames.Waist) },

                    { BoneNames.Thigh_L,     (BoneNames.None, BoneNames.None, BoneNames.Hip) },
                    { BoneNames.Knee_L,   (BoneNames.None, BoneNames.None, BoneNames.Thigh_L) },
                    { BoneNames.Ankle_L,   (BoneNames.None, BoneNames.None, BoneNames.Knee_L) },   

                    { BoneNames.Thigh_R,     (BoneNames.None, BoneNames.None, BoneNames.Hip) },
                    { BoneNames.Knee_R,   (BoneNames.None, BoneNames.None, BoneNames.Thigh_R) },
                    { BoneNames.Ankle_R,   (BoneNames.None, BoneNames.None, BoneNames.Knee_R) },   

                    { BoneNames.Shoulder_L,     (BoneNames.Chest, BoneNames.None, BoneNames.Waist) },
                    { BoneNames.Arm_L,     (BoneNames.Shoulder_L, BoneNames.Chest, BoneNames.Waist) },
                    { BoneNames.Elbow_L,   (BoneNames.None, BoneNames.None, BoneNames.Arm_L) },
                    { BoneNames.Wrist_L,   (BoneNames.None, BoneNames.None, BoneNames.Elbow_L) },       

                    { BoneNames.Shoulder_R,     (BoneNames.Chest, BoneNames.None, BoneNames.Waist) },
                    { BoneNames.Arm_R,     (BoneNames.Shoulder_R, BoneNames.Chest, BoneNames.Waist) },
                    { BoneNames.Elbow_R,   (BoneNames.None, BoneNames.None, BoneNames.Arm_R) },
                    { BoneNames.Wrist_R,   (BoneNames.None, BoneNames.None, BoneNames.Elbow_R) },

                    { BoneNames.Thumb_01_L, (BoneNames.Wrist_L, BoneNames.None, BoneNames.None) },
                    { BoneNames.Thumb_03_L, (BoneNames.Thumb_01_L, BoneNames.None, BoneNames.None) },

                    { BoneNames.Index_01_L, (BoneNames.Wrist_L, BoneNames.None, BoneNames.None) },
                    { BoneNames.Index_03_L, (BoneNames.Index_01_L, BoneNames.None, BoneNames.None) },
                    
                    { BoneNames.Ring_01_L, (BoneNames.Wrist_L, BoneNames.None, BoneNames.None) },
                    { BoneNames.Ring_03_L, (BoneNames.Ring_01_L, BoneNames.None, BoneNames.None) },    

                    { BoneNames.Thumb_01_R, (BoneNames.Wrist_R, BoneNames.None, BoneNames.None) },
                    { BoneNames.Thumb_03_R, (BoneNames.Thumb_01_R, BoneNames.None, BoneNames.None) },

                    { BoneNames.Index_01_R, (BoneNames.Wrist_R, BoneNames.None, BoneNames.None) },
                    { BoneNames.Index_03_R, (BoneNames.Index_01_R, BoneNames.None, BoneNames.None) },
                    
                    { BoneNames.Ring_01_R, (BoneNames.Wrist_R, BoneNames.None, BoneNames.None) },
                    { BoneNames.Ring_03_R, (BoneNames.Ring_01_R, BoneNames.None, BoneNames.None) },   

                    //Bone doesn't used in Mini
                    { BoneNames.Spine,  (BoneNames.None, BoneNames.None, BoneNames.None) },
                    { BoneNames.Ankle_offset_L,   (BoneNames.None, BoneNames.None, BoneNames.None) },
                    { BoneNames.Toe_L,   (BoneNames.None, BoneNames.None, BoneNames.None) },
                    { BoneNames.Toe_offset_L,   (BoneNames.None, BoneNames.None, BoneNames.None) },

                    { BoneNames.Ankle_offset_R,   (BoneNames.None, BoneNames.None, BoneNames.None) },
                    { BoneNames.Toe_R,   (BoneNames.None, BoneNames.None, BoneNames.None) },
                    { BoneNames.Toe_offset_R,   (BoneNames.None, BoneNames.None, BoneNames.None) },

                    { BoneNames.Pinky_01_L, (BoneNames.None, BoneNames.None, BoneNames.None) },
                    { BoneNames.Pinky_02_L, (BoneNames.None, BoneNames.None, BoneNames.None) },
                    { BoneNames.Pinky_03_L, (BoneNames.None, BoneNames.None, BoneNames.None) },

                    { BoneNames.Middle_01_L, (BoneNames.None, BoneNames.None, BoneNames.None) },
                    { BoneNames.Middle_02_L, (BoneNames.None, BoneNames.None, BoneNames.None) },
                    { BoneNames.Middle_03_L, (BoneNames.None, BoneNames.None, BoneNames.None) },

                    { BoneNames.Thumb_02_L, (BoneNames.None, BoneNames.None, BoneNames.None) },
                    { BoneNames.Index_02_L, (BoneNames.None, BoneNames.None, BoneNames.None) },
                    { BoneNames.Ring_02_L, (BoneNames.None, BoneNames.None, BoneNames.None) },
                };
            }
            else
            {
                boneParentDictionary = new Dictionary<BoneNames, (BoneNames optionParent1, BoneNames optionParent2, BoneNames necessaryParent)>()
                {
                    { BoneNames.Hip, (BoneNames.None, BoneNames.None, BoneNames.Position) },
                    { BoneNames.Waist,   (BoneNames.None, BoneNames.None, BoneNames.Hip) },
                    { BoneNames.Spine,  (BoneNames.None, BoneNames.None, BoneNames.Waist) },
                    { BoneNames.Chest,  (BoneNames.None, BoneNames.None, BoneNames.Spine) },

                    { BoneNames.Tail_Ctrl,   (BoneNames.None, BoneNames.None, BoneNames.Hip) },
                    { BoneNames.Neck,       (BoneNames.Chest, BoneNames.None, BoneNames.Spine) },
                    { BoneNames.Head,       (BoneNames.Neck, BoneNames.Chest, BoneNames.Spine) },

                    { BoneNames.Thigh_L,     (BoneNames.None, BoneNames.None, BoneNames.Hip) },
                    { BoneNames.Knee_L,   (BoneNames.None, BoneNames.None, BoneNames.Thigh_L) },
                    { BoneNames.Ankle_L,   (BoneNames.None, BoneNames.None, BoneNames.Knee_L) },
                    { BoneNames.Ankle_offset_L,   (BoneNames.None, BoneNames.None, BoneNames.Ankle_L) },
                    { BoneNames.Toe_L,   (BoneNames.None, BoneNames.None, BoneNames.Ankle_offset_L) },
                    { BoneNames.Toe_offset_L,   (BoneNames.None, BoneNames.None, BoneNames.Toe_L) },

                    { BoneNames.Thigh_R,     (BoneNames.None, BoneNames.None, BoneNames.Hip) },
                    { BoneNames.Knee_R,   (BoneNames.None, BoneNames.None, BoneNames.Thigh_R) },
                    { BoneNames.Ankle_R,   (BoneNames.None, BoneNames.None, BoneNames.Knee_R) },
                    { BoneNames.Ankle_offset_R,   (BoneNames.None, BoneNames.None, BoneNames.Ankle_R) },
                    { BoneNames.Toe_R,   (BoneNames.None, BoneNames.None, BoneNames.Ankle_offset_R) },
                    { BoneNames.Toe_offset_R,   (BoneNames.None, BoneNames.None, BoneNames.Toe_R) },

                    { BoneNames.Shoulder_L,     (BoneNames.Chest, BoneNames.None, BoneNames.Spine) },
                    { BoneNames.Arm_L,     (BoneNames.Shoulder_L, BoneNames.Chest, BoneNames.Spine) },
                    { BoneNames.Elbow_L,   (BoneNames.None, BoneNames.None, BoneNames.Arm_L) },
                    { BoneNames.Wrist_L,   (BoneNames.None, BoneNames.None, BoneNames.Elbow_L) },
                    
                    { BoneNames.Shoulder_R,     (BoneNames.Chest, BoneNames.None, BoneNames.Spine) },
                    { BoneNames.Arm_R,     (BoneNames.Shoulder_R, BoneNames.Chest, BoneNames.Spine) },
                    { BoneNames.Elbow_R,   (BoneNames.None, BoneNames.None, BoneNames.Arm_R) },
                    { BoneNames.Wrist_R,   (BoneNames.None, BoneNames.None, BoneNames.Elbow_R) },

                    { BoneNames.Thumb_01_L, (BoneNames.Wrist_L, BoneNames.None, BoneNames.None) },
                    { BoneNames.Thumb_02_L, (BoneNames.Thumb_01_L, BoneNames.None, BoneNames.None) },
                    { BoneNames.Thumb_03_L, (BoneNames.Thumb_02_L, BoneNames.None, BoneNames.None) },

                    { BoneNames.Index_01_L, (BoneNames.Wrist_L, BoneNames.None, BoneNames.None) },
                    { BoneNames.Index_02_L, (BoneNames.Index_01_L, BoneNames.None, BoneNames.None) },
                    { BoneNames.Index_03_L, (BoneNames.Index_02_L, BoneNames.None, BoneNames.None) },

                    { BoneNames.Middle_01_L, (BoneNames.Wrist_L, BoneNames.None, BoneNames.None) },
                    { BoneNames.Middle_02_L, (BoneNames.Middle_01_L, BoneNames.None, BoneNames.None) },
                    { BoneNames.Middle_03_L, (BoneNames.Middle_02_L, BoneNames.None, BoneNames.None) },
                    
                    { BoneNames.Ring_01_L, (BoneNames.Wrist_L, BoneNames.None, BoneNames.None) },
                    { BoneNames.Ring_02_L, (BoneNames.Ring_01_L, BoneNames.None, BoneNames.None) },
                    { BoneNames.Ring_03_L, (BoneNames.Ring_02_L, BoneNames.None, BoneNames.None) },

                    { BoneNames.Pinky_01_L, (BoneNames.Wrist_L, BoneNames.None, BoneNames.None) },
                    { BoneNames.Pinky_02_L, (BoneNames.Pinky_01_L, BoneNames.None, BoneNames.None) },
                    { BoneNames.Pinky_03_L, (BoneNames.Pinky_02_L, BoneNames.None, BoneNames.None) },

                    { BoneNames.Thumb_01_R, (BoneNames.Wrist_R, BoneNames.None, BoneNames.None) },
                    { BoneNames.Thumb_02_R, (BoneNames.Thumb_01_R, BoneNames.None, BoneNames.None) },
                    { BoneNames.Thumb_03_R, (BoneNames.Thumb_02_R, BoneNames.None, BoneNames.None) },

                    { BoneNames.Index_01_R, (BoneNames.Wrist_R, BoneNames.None, BoneNames.None) },
                    { BoneNames.Index_02_R, (BoneNames.Index_01_R, BoneNames.None, BoneNames.None) },
                    { BoneNames.Index_03_R, (BoneNames.Index_02_R, BoneNames.None, BoneNames.None) },

                    { BoneNames.Middle_01_R, (BoneNames.Wrist_R, BoneNames.None, BoneNames.None) },
                    { BoneNames.Middle_02_R, (BoneNames.Middle_01_R, BoneNames.None, BoneNames.None) },
                    { BoneNames.Middle_03_R, (BoneNames.Middle_02_R, BoneNames.None, BoneNames.None) },
                    
                    { BoneNames.Ring_01_R, (BoneNames.Wrist_R, BoneNames.None, BoneNames.None) },
                    { BoneNames.Ring_02_R, (BoneNames.Ring_01_R, BoneNames.None, BoneNames.None) },
                    { BoneNames.Ring_03_R, (BoneNames.Ring_02_R, BoneNames.None, BoneNames.None) },

                    { BoneNames.Pinky_01_R, (BoneNames.Wrist_R, BoneNames.None, BoneNames.None) },
                    { BoneNames.Pinky_02_R, (BoneNames.Pinky_01_R, BoneNames.None, BoneNames.None) },
                    { BoneNames.Pinky_03_R, (BoneNames.Pinky_02_R, BoneNames.None, BoneNames.None) },
                };
            }

            //Ghostの生成
            foreach (BoneNames boneName in boneDictionary.Keys)
            {
                if (boneName == BoneNames.Position)
                {
                    continue;
                }

                if (boneDictionary[boneName] == null)
                {
                    GhostDictionary.Add(boneName, (null, false));
                    continue;
                }

                Transform ghost = new GameObject(boneDictionary[boneName].name + GhostSalt).transform;
                if (boneName == BoneNames.Hip && UseBottomCenter)
                {
                    ghost.position = boneDictionary[BoneNames.Position].position;
                }
                else
                {
                    ghost.position = boneDictionary[boneName].position;
                }
                GhostDictionary.Add(boneName, (ghost, true));
            }

            //Ghostの親子構造を設定
            foreach (BoneNames boneName in boneDictionary.Keys)
            {
                if (boneName == BoneNames.Position)
                {
                    continue;
                }

                if (GhostDictionary[boneName].ghost == null || !GhostDictionary[boneName].enabled)
                {
                    continue;
                }

                if (boneName == BoneNames.Hip)
                {
                    GhostDictionary[boneName].ghost.SetParent(boneDictionary[BoneNames.Position]);
                    continue;
                }

                if (boneParentDictionary[boneName].optionParent1 != BoneNames.None && boneDictionary[boneParentDictionary[boneName].optionParent1] != null)
                {
                    GhostDictionary[boneName].ghost.SetParent(GhostDictionary[boneParentDictionary[boneName].optionParent1].ghost);
                }
                else if (boneParentDictionary[boneName].optionParent2 != BoneNames.None && boneDictionary[boneParentDictionary[boneName].optionParent2] != null)
                {
                    GhostDictionary[boneName].ghost.SetParent(GhostDictionary[boneParentDictionary[boneName].optionParent2].ghost);
                }
                else if (boneParentDictionary[boneName].necessaryParent != BoneNames.None && boneDictionary[boneParentDictionary[boneName].necessaryParent] != null)
                {
                    GhostDictionary[boneName].ghost.SetParent(GhostDictionary[boneParentDictionary[boneName].necessaryParent].ghost);
                }
                else
                {
                    GhostDictionary[boneName] = (GhostDictionary[boneName].ghost, false);
                }
            }

            //初期状態を保存
            foreach (BoneNames boneName in GhostDictionary.Keys)
            {
                if (GhostDictionary[boneName].ghost == null || !GhostDictionary[boneName].enabled)
                {
                    GhostOriginalLocalPositionDictionary.Add(boneName, Vector3.zero);
                    GhostOriginalRotationDictionary.Add(boneName, Quaternion.identity);
                    OriginalRotationDictionary.Add(boneName, Quaternion.identity);
                }
                else
                {
                    GhostOriginalRotationDictionary.Add(boneName, GhostDictionary[boneName].ghost.rotation);
                    OriginalRotationDictionary.Add(boneName, boneDictionary[boneName].rotation);
                    if (boneName == BoneNames.Hip && UseBottomCenter)
                    {
                        GhostOriginalLocalPositionDictionary.Add(boneName, Vector3.zero);
                        continue;
                    }
                    GhostOriginalLocalPositionDictionary.Add(boneName, GhostDictionary[boneName].ghost.localPosition);
                }
            }

            centerOffsetLength = Vector3.Distance(boneDictionary[BoneNames.Position].position, boneDictionary[BoneNames.Hip].position);
        }

        public void GhostAll()
        {
            foreach (BoneNames boneName in GhostDictionary.Keys)
            {
                if (GhostDictionary[boneName].ghost == null || !GhostDictionary[boneName].enabled) { continue; }
                Quaternion transQuaternion = boneDictionary[boneName].rotation * Quaternion.Inverse(OriginalRotationDictionary[boneName]);
                GhostDictionary[boneName].ghost.rotation = transQuaternion * GhostOriginalRotationDictionary[boneName];
                if (boneName == BoneNames.Hip && UseBottomCenter)
                {
                    GhostDictionary[boneName].ghost.position = boneDictionary[boneName].position - centerOffsetLength * GhostDictionary[boneName].ghost.up;
                    continue;
                }
                GhostDictionary[boneName].ghost.position = boneDictionary[boneName].position;
            }
        }
    }
}