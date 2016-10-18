服务安装
	1、运行生成xmmapMonitor.exe文件
	2、用管理员身份打开命令行
		2.1、win+r
		2.2、输入cmd 并回车
		2.3  输入runas /noprofile /user:Administrator cmd 并回车
		或者
		右键win按钮，找到以管理员成分运行
	3、安装
		3.1、找到installUtil.exe的路径，一般而言为：C:\Windows\Microsoft.NET\Framework64\v4.0.30319\InstallUtil.exe
		3.2、找到xmmapMonitor.exe的生成路径，因人而异 如：E:\visualstudio2010Projects\xmmapMonitor\xmmapMonitor\bin\Debug\xmmapMonitor.exe
		3.3、
			安装命令：C:\Windows\Microsoft.NET\Framework64\v4.0.30319\InstallUtil.exe  E:\visualstudio2010Projects\xmmapMonitor\xmmapMonitor\bin\Debug\xmmapMonitor.exe
	4、卸载
		C:\Windows\Microsoft.NET\Framework64\v4.0.30319\InstallUtil.exe  /u E:\visualstudio2010Projects\xmmapMonitor\xmmapMonitor\bin\Debug\xmmapMonitor.exe
	5、打开服务
		5.1、ctrl+alt+del
		5.2、在最上方菜单找到服务，并在服务页面点击“打开服务”
		5.3、找到ssmapMonitor.exe服务右键运行