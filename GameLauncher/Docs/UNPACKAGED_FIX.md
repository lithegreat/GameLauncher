# �޸�δ���ģʽ�µ����ݴ洢����

## ��������
��δ���ģʽ������ GameLauncher ʱ��ִ�����²�������ִ���
- ������Ϸ
- �����Ϸ  
- ������Ϸ
- ɾ����Ϸ

������Ϣ��`������Ϸ����ʧ��: Operation is not valid due to the current state of the object.`

## �������
��δ���ģʽ�£�`ApplicationData.Current` �����ã��⵼�����´���ʧ�ܣ�
1. `ThemeService.cs` �е��������ñ���
2. `GamesPage.xaml.cs` �е���Ϸ���ݱ���

## �������

### 1. ����ͳһ���ݴ洢���� (DataStorageService)
������ `GameLauncher\Services\DataStorageService.cs`���ṩ����/δ���ģʽ�����ݴ洢���ܣ�

- **�Զ����ģʽ**���Զ����Ӧ���Ƿ��ڴ��ģʽ������
- **���ܽ���**���ڴ��ģʽʧ��ʱ�Զ��������ļ�ϵͳ�洢
- **ͳһAPI**���ṩһ�µĶ�д�ӿڣ�������ĵײ�ʵ��

### 2. ����������� (ThemeService)
���� `ThemeService.cs` ʹ���µ����ݴ洢����
- �Ƴ��� `ApplicationData.Current.LocalSettings` ��ֱ������
- ʹ�� `DataStorageService.ReadSetting/WriteSetting` ����

### 3. ������Ϸҳ�� (GamesPage)
���� `GamesPage.xaml.cs` �е����ݱ����߼���
- �Ƴ��� `ApplicationData.Current.LocalFolder` ��ֱ������
- ʹ�� `DataStorageService.ReadTextFileAsync/WriteTextFileAsync` ����

### 4. �Ż���Ŀ����
�� `GameLauncher.csproj` ����ӣ�
- `WindowsAppSDKSelfContained=true`��ȷ������ʱ����԰���
- `WindowsPackageType=None`����ȷָ��δ���ģʽ
- `DisableXbfLineInfo=true`�����δ���ģʽ������

### 5. ��ǿӦ���嵥
���� `app.manifest`��
- ���������Ϣ�Է�ֹ COM ��ȫ����
- ���ó�·��֧��
- �ϲ��ظ��� windowsSettings Ԫ��

## ���ݴ洢λ��

### ���ģʽ
- ���ã�`ApplicationData.Current.LocalSettings`
- �ļ���`ApplicationData.Current.LocalFolder`

### δ���ģʽ
- ���ã�`%LocalAppData%\GameLauncher\settings.ini`
- �ļ���`%LocalAppData%\GameLauncher\`
- ���ã�Ӧ�ó���Ŀ¼�µ� `Data` �ļ���

## ������֤
�� `App.xaml.cs` �����������ʱ�����ݴ洢������ԣ�ȷ������������ģʽ�¶�������������

## ��������
- ���������������ݸ�ʽ����ȫ����
- �ڴ��ģʽ������ʹ��ԭ�е� ApplicationData API
- ���ڱ�Ҫʱ�������ļ�ϵͳ�洢

## ������
- ��ǿ�˴�����־��¼
- �ṩ����ϸ�ĵ�����Ϣ
- �ڴ洢ʧ��ʱ���û���ȷ�Ĵ�����ʾ

## ʹ�÷���
������Զ��������ģʽ�������������ֶ��л���

```csharp
// ��ȡ����
var theme = DataStorageService.ReadSetting("AppTheme", "Default");

// д������
DataStorageService.WriteSetting("AppTheme", "Dark");

// ��ȡ�ļ�
var json = await DataStorageService.ReadTextFileAsync("games.json");

// д���ļ�
await DataStorageService.WriteTextFileAsync("games.json", jsonContent);
```

�������������׽����δ���ģʽ�µ����ݴ洢���⣬ͬʱ�����˶Դ��ģʽ������֧�֡�