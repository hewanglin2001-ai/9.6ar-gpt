# 无脸皮肤重建：测试版本

## 打开与测试

1. Unity 停止播放；GitHub Desktop 选中 9.6ar-gpt 的 main，Fetch origin 后 Pull origin。
2. 回到 Unity 6000.0.58f2，等待导入。若提示场景变更，选择 Reload。
3. 打开 Assets/MediaPipeUnity/Samples/Scenes/Face Landmark Detection/Face Landmark Detection.unity。
4. 点击播放（Play），允许摄像头。MidFaceEffect 已连接两个 Shader，无需手动拖拽。
5. 默认直接显示最终皮肤。右上角 Erase 滑块可以对比原画面。
6. Final skin 显示最终效果；Replay entry / R 重播渐变；H 隐藏或显示小面板。
   快捷键需要先点击游戏（Game）画面取得焦点。
7. 468 landmarks 显示原始跟踪点；Regions + boundary 显示局部区域、中心点、羽化边缘和人脸边界框。

先比较正脸和轻微左右转头，再判断质感是否达到艺术要求。当前版本没有经过用户电脑的 Unity / Metal / 摄像头运行验证。
不要把代码检查通过等同于已经达到参考图。

## 这一版的处理方法

- 原视频由原 RawImage 以原分辨率显示。只替换局部特效涉及的颜色。
- 七个局部区域覆盖眉眼、眉间鼻梁、鼻子、鼻唇之间、嘴和中央下巴。
  眉眼范围包含眼眶下缘；嘴部根据外唇关键点定义，闭嘴时不会塌成细线。
- 原脸外轮廓只作向内收缩的安全限制，不作为产生覆盖的 Face Oval 蒙版。
- 在滤波前将五官区域的来源权重设为零，避免把眼睛、牙齿、鼻孔扩散成重影。
- 六处脸颊小区域只用于采样有效性判断和缺少来源时的后备值；主体来自可信皮肤像素的空间分布。
- GPU 浮点置信度金字塔、连续可分离 Gaussian、逐级回填和边界松弛生成连续皮肤。
- 重建皮肤在跟随脸部的坐标系里进行短时平滑。原视频和最终遮罩边缘不降采样。
- 各局部区域先合并透明度，再合成一次，避免网格叠色接缝。
- 默认轻微宽高光和低幅度颗粒，可以在 MidFaceEffect 检查器中调整 Volume / Fine Grain。
- 组件禁用时恢复视频材质及关键点可见性，释放 RenderTexture 和临时材质。

## 入场与后续植物接口

入场默认关闭，先验收最终皮肤。Replay entry 打开后：

| 时间 | 变化 |
| --- | --- |
| 0–3 秒 | 原始人脸 |
| 3–6 秒 | 眉眼逐渐消失 |
| 6–9 秒 | 鼻梁和鼻子逐渐消失 |
| 9–12 秒 | 嘴部逐渐消失 |
| 12–15 秒 | 中央下巴补全 |
| 15–24 秒 | GrowthProgress 从 0 到 1，供后续植物驱动 |

这版未加入植物模型或生长渲染。已预留：

- FrameUpdated 事件、IsTracking、FacePresence、PresentationSeconds、GrowthReady、GrowthProgress。
- TryGetSurfaceAnchor(landmarkId, out Pose, out float faceWidthWorld)。
  建议锚点 168（鼻梁）、6（两眼之间）、0（嘴上方）。
  位置映射到实际视频表面，旋转来自 MediaPipe 头部姿态并按预览镜像修正。
  植物网格朝向、深度和比例仍需导入模型后校准。
- HeadPose 为当前 MediaPipe 转换后的 Unity 矩阵，方便后续更完整的三维对齐。
- 丢脸后皮肤渐退；离开超过 1.2 秒后重新进入会重置入场时间。

## 已做检查与限制

Tools/Faceless/verify_skin.py 将项目中实际片元函数体转换为 GLSL，通过 Mesa 执行。
以 skimage 的 NASA Eileen Collins 人像和 MediaPipe 检测点作为测试输入；
测试素材不随仓库提交。脚本依赖及调用参数见文件头。

检查内容包括 8 个片元程序编译执行、输出没有 NaN/Inf、关闭效果时原图不变、
人脸边界框之外的像素不变，以及眉眼、鼻子、嘴唇采样位置的来源权重为零。
它不测试 Unity 的 C# API 链接、UI 顶点处理、摄像头镜像链路或 Metal 编译。

参考图是单张处理后的图像。实时输入中的极端侧脸、头发/手遮住五官、
眼镜、大面积胡须和强烈侧光尚未实现专门的分割与补全；
这些情况下可能仍有边缘痕迹或明暗差异。当前局部轮廓限制也会保留真实侧脸的鼻子外轮廓。
镜像、转头、离开再进入、帧率和最终质感必须在实际展览电脑上继续验收。
