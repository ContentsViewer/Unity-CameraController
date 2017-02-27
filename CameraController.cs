/*
*プログラム: CameraController
*   最終更新日:
*       12.5.2016
*
*   説明:
*       カメラを操作します
*       一人称視点,三人称視点切り替えができます
*       カメラ位置,カメラ方向は外部ゲームオブジェクトから指定できます
*       カメラ位置移動,カメラ視点移動のときに線形補間,曲線補間ができます
*
*   必須:
*       GameObject:
*           attacheObjectPlayer:
*               一人称視点の位置向きを知るのに使います
*               このゲームオブジェクトの位置が一人称視点の位置になります
*               このゲームオブジェクトのforwardベクトルの向きが一人称視点の方向になります
*
*           attacheObjectObserver:
*               三人称視点の位置向きを知るのに使います
*               このゲームオブジェクトの位置が三人称視点の位置になります
*               このゲームオブジェクトのforwardベクトルの向きが三人称視点の方向になります
*
*   更新履歴:
*       2.22.2016:
*           プログラムの完成
*
*       3.12.2016; 3.18.2016:
*           スクリプト修正
*
*       3.23.2016:
*           autoAvoidCollider機能追加
*
*       3.27.2016:
*           スクリプト修正
*
*       3.31.2016:
*           POSITION,DIRECTIONでのCUSTUMモードの仕様変更
*           DIRECTION内にSTATIONモードを追加
*           DIRECTION.GAZEモードを実装
*
*       4.12.2016; 4.17.2016; 5.15.2016:
*           スクリプト修正
*
*       7.7.2016:
*           AutoAvoidColliderが有効時, TargetObjectが設定されているならばそのObjectを用い他処理を優先するように変更
*
*       7.22.2016:
*           CameraのZ軸方向の回転に対応
*
*       8.2.2016:
*           スクリプト修正
*
*       8.3.2016:
*           クラス名変更
*           
*       12.5.2016:
*           フレームの違いによらず, 速度を一定にするようにした.
*
*/


using UnityEngine;
using System.Collections;


public class CameraController : MonoBehaviour
{

    //カメラのポジションタイプ
    public enum CAMERA_POSITION
    {
        PLAYER,
        OBSERVER,
        STATION,
        CUSTUM
    }

    //カメラの回転
    public enum CAMERA_ROTATION
    {
        PLAYER,
        OBSERVER,
        GAZE,
        STATION,
        CUSTOM
    }

    //カメラのホーミングタイプ(POSITION)
    public enum CAMERA_HOMING_POSITION
    {
        DIRECT,
        LERP,
        SLERP,
        STOP
    }

    //カメラのホーミングタイプ(DIRECTION)
    public enum CAMERA_HOMING_ROTATION
    {
        DIRECT,
        LERP,
        SLERP,
        STOP
    }

    //===外部パラメータ(Inspector表示)=======================================
    public string attacheObjectPlayer = "CameraPointPlayer";
    public string attacheObjectObserver = "CameraPointObserver";

    [Space(10)]
    public LayerMask layerMaskCollider = ~0x00;
    public float distanceFromCollider = 0.1f;
    public float escapeFromTrap = 3.0f;

    [System.Serializable]
    public class Param
    {
        public bool autoAvoidCollider = true;

        [Space(10)]
        public CAMERA_POSITION positionType = CAMERA_POSITION.PLAYER;
        public CAMERA_ROTATION rotationType = CAMERA_ROTATION.PLAYER;

        [Space(10)]
        public CAMERA_HOMING_POSITION homingTypePosition = CAMERA_HOMING_POSITION.DIRECT;
        public CAMERA_HOMING_ROTATION homingTypeRotation = CAMERA_HOMING_ROTATION.DIRECT;

        [Space(10)]
        public Vector3 homingPosition = new Vector3(2.0f,2.0f,2.0f);
        public float homingRotation = 20.0f;

        [Space(10)]
        public Transform station;
        public Transform target;
        public Transform gazeAt;

        [Space(10)]
        public Vector3 cameraRotation = new Vector3(0.0f, 0.0f, 0.0f);
        public Vector3 cameraPosition = new Vector3(0.0f, 0.0f, 0.0f);
    }
    public Param param;

    //===外部パラメータ=======================================================
    public GameObject cameraPointPlayerObject
    {
        get
        {
            return cameraPointPlayer;
        }
    }

    public GameObject cameraPointObserverObject
    {
        get
        {
            return cameraPointObserver;
        }
    }

    //===内部パラメータ=====================================================


    //===キャッシュ=========================================================
    GameObject cameraPointPlayer;
    Transform cameraPointPlayerTrfm;

    GameObject cameraPointObserver;
    Transform cameraPointObserverTrfm;

    Camera camera;

    //===コード==============================================================
    void Awake()
    {
        camera = GetComponent<Camera>();

        //cameraPointPlayer
        cameraPointPlayer = GameObject.Find(attacheObjectPlayer);
        if (cameraPointPlayer)
        {
            cameraPointPlayerTrfm = cameraPointPlayer.transform;
        }
        else
        {
            Debug.LogWarning(string.Format("[CameraFollow.Awake] GameObject'{0}'が見つかりませんでした", attacheObjectPlayer));
        }

        //cameraPointObserver
        cameraPointObserver = GameObject.Find(attacheObjectObserver);
        if (cameraPointObserver)
        {
            cameraPointObserverTrfm = cameraPointObserver.transform;
        }
        else
        {
            Debug.LogWarning(string.Format("[CameraFollow.Awake] GameObject'{0}'が見つかりませんでした", attacheObjectObserver));
        }
    }

    void LateUpdate()
    {
        Vector3 targetPosition = param.cameraPosition;
        Vector3 position = transform.position;

        Quaternion targetRotation = Quaternion.Euler(param.cameraRotation);
        Quaternion rotation = transform.rotation;


        //ターゲットの設定
        switch (param.positionType)
        {
            case CAMERA_POSITION.PLAYER:
                if (cameraPointPlayer)
                {
                    targetPosition = cameraPointPlayerTrfm.position;
                }
                break;

            case CAMERA_POSITION.OBSERVER:
                if (cameraPointObserver)
                {
                    targetPosition = cameraPointObserverTrfm.position;
                }
                break;

            case CAMERA_POSITION.STATION:
                if (param.station)
                {
                    targetPosition = param.station.position;
                }
                break;

            case CAMERA_POSITION.CUSTUM:
                targetPosition = param.cameraPosition;
                break;
        }

        //カメラの方向設定
        switch (param.rotationType)
        {
            case CAMERA_ROTATION.PLAYER:
                if (cameraPointPlayer)
                {
                    targetRotation = cameraPointPlayerTrfm.rotation;
                }
                break;

            case CAMERA_ROTATION.OBSERVER:
                if (cameraPointObserver)
                {
                    targetRotation = cameraPointObserverTrfm.rotation;
                }
                break;

            case CAMERA_ROTATION.GAZE:
                if (param.gazeAt)
                {
                    targetRotation = Quaternion.FromToRotation(Vector3.forward, param.gazeAt.position - transform.position);
                }
                break;

            case CAMERA_ROTATION.STATION:
                if (param.station)
                {
                    targetRotation = param.station.rotation;
                }
                break;

            case CAMERA_ROTATION.CUSTOM:
                targetRotation = Quaternion.Euler(param.cameraRotation);
                break;
        }

        //コライダーを避ける
        if (param.autoAvoidCollider)
        {
            //TargetObjectとの間に壁があるときTargetが見えるようにカメラが移動する
            //TargetObjectからCamera移動後の位置にレーザーを飛ばしその間にコライダーがある場合そのコライダーの手前にカメラを移動する
            //レイヤーマスクに登録されているコライダーを判定の対象にする
            if (param.target)
            {
                RaycastHit hit;
                Transform targetTrfm = param.target.transform;
                if (Physics.Linecast(targetTrfm.position, targetPosition, out hit, layerMaskCollider))
                {
                    targetPosition = targetTrfm.position + ((targetPosition - targetTrfm.position) / (targetPosition - targetTrfm.position).magnitude) * (hit.distance - distanceFromCollider);
                }
            }
            else
            {
                //コライダーを抜けないようにする
                //レイヤーマスクに登録されているコライダーを判定の対象にする
                {
                    RaycastHit hit;
                    if (Physics.Linecast(transform.position, targetPosition, out hit, layerMaskCollider))
                    {
                        if (Vector3.Distance(targetPosition, transform.position) < escapeFromTrap)
                        {
                            targetPosition = transform.position + ((targetPosition - transform.position) / (targetPosition - transform.position).magnitude) * (hit.distance - distanceFromCollider);
                        }
                    }
                }

                //視野で壁が抜けないようにする
                {
                    Vector3[] fieldOfViewVecList = new Vector3[9];
                    float near = camera.nearClipPlane;
                    float fieldOfViewRadian = camera.fieldOfView * (3.14f / 180.0f);
                    Vector3 forwardVec = (transform.forward / transform.forward.magnitude) * near;
                    Vector3 upVec = (transform.up / transform.up.magnitude) * near * Mathf.Tan(fieldOfViewRadian / 2.0f);
                    Vector3 rightVec = (transform.right / transform.right.magnitude) * near * Mathf.Tan(fieldOfViewRadian / 2.0f) * camera.aspect;
                    fieldOfViewVecList[0] = forwardVec + upVec + rightVec;
                    fieldOfViewVecList[1] = forwardVec - upVec + rightVec;
                    fieldOfViewVecList[2] = forwardVec + upVec - rightVec;
                    fieldOfViewVecList[3] = forwardVec - upVec - rightVec;
                    fieldOfViewVecList[4] = forwardVec + upVec;
                    fieldOfViewVecList[5] = forwardVec - upVec;
                    fieldOfViewVecList[6] = forwardVec + rightVec;
                    fieldOfViewVecList[7] = forwardVec - rightVec;
                    fieldOfViewVecList[8] = forwardVec;
                    foreach (Vector3 vec in fieldOfViewVecList)
                    {
                        RaycastHit hit;

                        //移動後の位置を用いて計算する
                        //targetから見て前に壁があるかどうか判別
                        Debug.DrawLine(targetPosition, targetPosition + vec, Color.red);
                        if (Physics.Linecast(targetPosition, targetPosition + vec, out hit, layerMaskCollider))
                        {
                            targetPosition -= vec / vec.magnitude * (vec.magnitude - hit.distance);
                        }
                    }
                }
            }
        }

        //カメラ移動(位置): ホーミング
        switch (param.homingTypePosition)
        {
            case CAMERA_HOMING_POSITION.DIRECT:
                position = targetPosition;
                break;

            case CAMERA_HOMING_POSITION.LERP:
                position.x = Mathf.Lerp(position.x, targetPosition.x, param.homingPosition.x * Time.deltaTime);
                position.y = Mathf.Lerp(position.y, targetPosition.y, param.homingPosition.y * Time.deltaTime);
                position.z = Mathf.Lerp(position.z, targetPosition.z, param.homingPosition.z * Time.deltaTime);
                break;

            case CAMERA_HOMING_POSITION.SLERP:
                position.x = Mathf.SmoothStep(position.x, targetPosition.x, param.homingPosition.x * Time.deltaTime);
                position.y = Mathf.SmoothStep(position.y, targetPosition.y, param.homingPosition.y * Time.deltaTime);
                position.z = Mathf.SmoothStep(position.z, targetPosition.z, param.homingPosition.z * Time.deltaTime);
                break;

            case CAMERA_HOMING_POSITION.STOP:
                break;
        }



        //カメラ移動(回転): ホーミング
        switch (param.homingTypeRotation)
        {
            case CAMERA_HOMING_ROTATION.DIRECT:
                rotation = targetRotation;
                break;

            case CAMERA_HOMING_ROTATION.LERP:
                rotation = Quaternion.Lerp(transform.rotation, targetRotation, param.homingRotation * Time.deltaTime);
                break;

            case CAMERA_HOMING_ROTATION.SLERP:
                rotation = Quaternion.Slerp(transform.rotation, targetRotation, param.homingRotation * Time.deltaTime);
                break;

            case CAMERA_HOMING_ROTATION.STOP:
                break;
        }

        //カメラ位置向き更新
        transform.position = position;
        transform.rotation = rotation;
    }

    public void SetCamera(Param cameraPara)
    {
        param = cameraPara;
    }
}
