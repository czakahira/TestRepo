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
/// Addressable�ւ̃��[�h���N�G�X�g�p�\����
/// </summary>
[Serializable]
public class RequestLoadAssetInfo
{
	/// <summary>
	/// ���[�h�������A�Z�b�g�̃L�[
	/// </summary>
	public object key;

	/// <summary>
	/// ���[�h�������ɌĂ΂��R�[���o�b�N
	/// </summary>
	[NonSerialized] public UnityAction<object> callback;

	public RequestLoadAssetInfo(object _key, UnityAction<object> _callback)
	{
		key = _key;
		callback = _callback;
	}
}

/// <summary>
/// Addressable�̃��[�_�[
/// <para>
/// �E���[�h�������A�Z�b�g�̃p�X�����N�G�X�g���A�J�n��ʒm���邱�ƂŎ����Ń��[�h���܂��B<br/>
/// �E���N�G�X�g�͕����\�ł�
/// </para>
/// </summary>
[Serializable]
public class AddressableLoader
{
	#region Member
	/// <summary>
	/// �o�^�ς݃��N�G�X�g
	/// </summary>
	[SerializeField] protected List<RequestLoadAssetInfo> m_RequestQueues;
	/// <summary>
	/// ���N�G�X�g�̑���
	/// </summary>
	protected uint m_RequestCount;
	/// <summary>
	/// ���[�h��
	/// </summary>
	protected EAddressableLoadState m_State;
	/// <summary>
	/// ���[�h���́u�n���h���E�R�[���o�b�N�v�y�A
	/// </summary>
	protected Dictionary<AsyncOperationHandle, UnityAction<object>> m_RunningOperations;
	/// <summary>
	/// ���[�h�������̃R�[���o�b�N
	/// </summary>
	protected event UnityAction m_OnComplete;
	#endregion

	#region Property
	/// <summary>
	/// ���[�h��
	/// </summary>
	public bool isLoading { get { return (m_State != EAddressableLoadState.None); } }
	/// <summary>
	/// ���N�G�X�g���P�ł����݂���
	/// </summary>
	public bool isRequested { get { return (m_RequestCount > 0); } }
	/// <summary>
	/// ���[�h����
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
	/// ���N�G�X�g�����̍���
	/// </summary>
	protected void RequestInternal(RequestLoadAssetInfo request)
	{
		m_RequestQueues.Add(request);
		m_RequestCount++;

		//���N�G�X�g���L�����Ƃ������̓��[�h�������ɂȂ�
		completed = false;
	}
	/// <summary>
	/// ���N�G�X�g
	/// </summary>
	/// <param name="_path"></param>
	/// <param name="_callback"></param>
	public void Request(string _path, UnityAction<object> _callback)
	{
		//���Ƀ��[�h���J�n���Ă���ꍇ�̓��N�G�X�g����
		if(isLoading) { return; }

		//���Ƀ��N�G�X�g�ς݂̃p�X�Ȃ�A�ǉ��͂����ɃR�[���o�b�N�������p��
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
		
		//���N�G�X�g�ɒǉ�
		RequestInternal(new RequestLoadAssetInfo(_path, _callback));
		Debug.Log($"Request {_path} / {Time.frameCount}");
	}

	protected void ClearRequest()
	{
		m_RequestQueues.Clear();
		m_RequestCount = 0;
	}

	/// <summary>
	/// ���[�h�J�n
	/// </summary>
	public void Start(UnityAction _onCompleted = null)
	{
		//���ݐi�s�`�Ń��[�h�����A���N�G�X�g���P���Ȃ��ꍇ�͊J�n�ł��Ȃ�
		if(isLoading || !isRequested) { return; }

		//�R�[���o�b�N�擾
		m_OnComplete += _onCompleted;

		m_RunningOperations.Clear();

		//�J�n
		NextState(EAddressableLoadState.ListUp); 
	}

	/// <summary>
	/// Update
	/// <para> �E�񓯊��Ȃ̂ŕԂ�l�̂Ȃ�Task�ł� </para>
	/// </summary>
	public async Task Update()
	{
		switch(m_State){
			//���N�G�X�g���ꂽ�p�X���n���h����S�ă��X�g�A�b�v����
			case EAddressableLoadState.ListUp:
				Debug.Log($"Queue : {m_RequestQueues.Count}");
				foreach (var queue in m_RequestQueues) {
					m_RunningOperations.Add(Addressables.LoadAssetAsync<object>(queue.key), queue.callback);
				}
				//for (int i = 0; i < m_RequestCount; i++) {
				//	m_RunnningOperations.Add(Addressables.LoadAssetAsync<object>(m_RequestQueues[i].key), m_RequestQueues[i].callback);
				//}
				Debug.Log($"Operation : {m_RunningOperations.Count} / {Time.frameCount}");
				//���[�h����
				foreach (var operation in m_RunningOperations) {
					AsyncOperationHandle handle = operation.Key;
					//���[�h�̏I����҂�
					await handle.Task;
					//���[�h�ɐ���������R�[���o�b�N���Ă�
					if (handle.Status == AsyncOperationStatus.Succeeded) {
						operation.Value?.Invoke(handle.Result);
					}
				}
				// �n���h�����J������
				foreach (var operation in m_RunningOperations) {
					Addressables.Release(operation.Key);
				}
				//�S�ăN���A
				m_RunningOperations.Clear();
				ClearRequest();
				//���[�h����
				completed = true;
				m_OnComplete?.Invoke();
				m_OnComplete = null;
				//�X�e�[�g��������
				NextState(EAddressableLoadState.None);
				break;

			//�������Ă��Ȃ�
			case EAddressableLoadState.None:
			default:
				break;
		}
	}

	/// <summary>
	/// ���̃X�e�[�g��
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
