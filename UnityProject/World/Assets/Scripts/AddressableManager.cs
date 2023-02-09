using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public enum EAddressableLoadState
{
	None,

	ListUp,
	Load,
	Release,

	All,
}

/// <summary>
/// Addressableへのロードリクエスト用構造体
/// </summary>
[Serializable]
public class RequestLoadAssetInfo
{
	/// <summary>
	/// ロードしたいアセットのキー
	/// </summary>
	public object key;

	/// <summary>
	/// ロード完了時に呼ばれるコールバック
	/// </summary>
	[NonSerialized] public UnityAction<object> callback;

	public RequestLoadAssetInfo(object _key, UnityAction<object> _callback)
	{
		key = _key;
		callback = _callback;
	}
}

/// <summary>
/// Addressableのローダー
/// <para>
/// ・ロードしたいアセットのパスをリクエストし、開始を通知することで自動でロードします。<br/>
/// ・リクエストは複数可能です
/// </para>
/// </summary>
[Serializable]
public class AddressableLoader
{
	#region Member
	/// <summary>
	/// 登録済みリクエスト
	/// </summary>
	[SerializeField] protected List<RequestLoadAssetInfo> m_RequestQueues;
	/// <summary>
	/// リクエストの総数
	/// </summary>
	protected uint m_RequestCount;
	/// <summary>
	/// ロード状況
	/// </summary>
	protected EAddressableLoadState m_State;
	/// <summary>
	/// ロード中の「ハンドル・コールバック」ペア
	/// </summary>
	protected Dictionary<AsyncOperationHandle, UnityAction<object>> m_RunningOperations;
	/// <summary>
	/// ロード完了時のコールバック
	/// </summary>
	protected event UnityAction m_OnComplete;
	#endregion

	#region Property
	/// <summary>
	/// ロード中
	/// </summary>
	public bool isLoading { get { return (m_State != EAddressableLoadState.None); } }
	/// <summary>
	/// リクエストが１つでも存在する
	/// </summary>
	public bool isRequested { get { return (m_RequestCount > 0); } }
	/// <summary>
	/// ロード完了
	/// </summary>
	public bool completed { get; protected set; }
	#endregion

	public AddressableLoader()
	{
		m_RequestQueues = new List<RequestLoadAssetInfo>();
		m_RequestCount = 0;
		m_State = EAddressableLoadState.None;
		m_RunningOperations = new Dictionary<AsyncOperationHandle, UnityAction<object>>();

		m_OnComplete = null;
		completed = false;
	}
	~AddressableLoader(){ }

	#region Method
	/// <summary>
	/// リクエスト処理の根幹
	/// </summary>
	protected void RequestInternal(RequestLoadAssetInfo request)
	{
		m_RequestQueues.Add(request);
		m_RequestCount++;

		//リクエストが有ったという事はロードも未完になる
		completed = false;
	}
	/// <summary>
	/// リクエスト
	/// </summary>
	/// <param name="_path"></param>
	/// <param name="_callback"></param>
	public void Request(string _path, UnityAction<object> _callback)
	{
		//既にロードが開始している場合はリクエスト拒否
		if(isLoading) { return; }

		//既にリクエスト済みのパスなら、追加はせずにコールバックを引き継ぐ
		if(isRequested){
			foreach (var queue in m_RequestQueues) {
				if (queue.key.Equals(_path)) {
					queue.callback += _callback;
					return;
				}
			}
			//for (int i = 0; i < m_RequestCount; i++) {
			//	if (m_RequestQueues[i].key.Equals(_path)) {
			//		m_RequestQueues[i].callback += _callback;
			//		return;
			//	}
			//}
		}
		
		//リクエストに追加
		RequestInternal(new RequestLoadAssetInfo(_path, _callback));
		Debug.Log($"Request {_path} / {Time.frameCount}");
	}

	protected void ClearRequest()
	{
		m_RequestQueues.Clear();
		m_RequestCount = 0;
	}

	/// <summary>
	/// ロード開始
	/// </summary>
	public void Start(UnityAction _onCompleted = null)
	{
		//現在進行形でロード中か、リクエストが１つもない場合は開始できない
		if(isLoading || !isRequested) { return; }

		//コールバック取得
		m_OnComplete += _onCompleted;

		m_RunningOperations.Clear();

		//開始
		NextState(EAddressableLoadState.ListUp); 
	}

	/// <summary>
	/// Update
	/// <para> ・非同期なので返り値のないTaskです </para>
	/// </summary>
	public async Task Update()
	{
		switch(m_State){
			//リクエストされたパスよりハンドルを全てリストアップする
			case EAddressableLoadState.ListUp:
				Debug.Log($"Queue : {m_RequestQueues.Count}");
				foreach (var queue in m_RequestQueues) {
					m_RunningOperations.Add(Addressables.LoadAssetAsync<object>(queue.key), queue.callback);
				}
				//for (int i = 0; i < m_RequestCount; i++) {
				//	m_RunnningOperations.Add(Addressables.LoadAssetAsync<object>(m_RequestQueues[i].key), m_RequestQueues[i].callback);
				//}
				Debug.Log($"Operation : {m_RunningOperations.Count} / {Time.frameCount}");
				//ロードする
				foreach (var operation in m_RunningOperations) {
					AsyncOperationHandle handle = operation.Key;
					//ロードの終了を待つ
					await handle.Task;
					//ロードに成功したらコールバックを呼ぶ
					if (handle.Status == AsyncOperationStatus.Succeeded) {
						operation.Value?.Invoke(handle.Result);
					}
				}
				// ハンドルを開放する
				foreach (var operation in m_RunningOperations) {
					Addressables.Release(operation.Key);
				}
				//全てクリア
				m_RunningOperations.Clear();
				ClearRequest();
				//ロード完了
				completed = true;
				m_OnComplete?.Invoke();
				m_OnComplete = null;
				//ステートも初期化
				NextState(EAddressableLoadState.None);
				break;

			//何もしていない
			case EAddressableLoadState.None:
			default:
				break;
		}
	}

	/// <summary>
	/// 次のステートへ
	/// </summary>
	/// <param name="_next"></param>
	protected void NextState(EAddressableLoadState _next){ m_State = _next; }
	#endregion

}

public class AddressableManager : MonoBehaviour
{
	[SerializeField] protected List<AddressableLoader> m_EnqueueLoader = new List<AddressableLoader>();

    // Start is called before the first frame update
    void Start()
    {
        
    }

	public AddressableLoader GetLoader()
	{
		AddressableLoader loader = new AddressableLoader();
		m_EnqueueLoader.Add(loader);
		return loader;
	}

	async void Update()
	{
		if(m_EnqueueLoader.Count > 0){ 
			Debug.LogWarning(m_EnqueueLoader.Count);
			foreach(AddressableLoader loader in m_EnqueueLoader){
				await loader.Update();
			}
		}

	}

}
