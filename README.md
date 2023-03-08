# UnitySoftRender
[![Badge](https://img.shields.io/badge/github-Zagara-lightgrey.svg)](https://github.com/justalittlefat/Zagara)
[![Badge](https://img.shields.io/badge/知乎-用300行代码写一个软渲染器-blue.svg)](https://zhuanlan.zhihu.com/p/33600502)
[![996.icu](https://img.shields.io/badge/link-996.icu-red.svg)](https://996.icu)
Unity软渲染，省掉了Light，Camera，Mesh等对象的实现，以及矩阵相关的数学代码。<br>代码所实现的，是从Unity获取到渲染数据后，输送到C#软渲染器，经历顶点着色器，三角面装配，光栅化，片段着色器，最终画出一帧的过程。
## 简介<br>

> 加了一点中文注释，便于理解<br>
> 重新组织了文件，便于初学者<br>
> 渲染分为不透明跟透明[透明渲染](##透明渲染) & [不透明渲染](##不透明渲染)

## 渲染流程
![](https://github.com/yqlizeao/UnitySoftRender/blob/master/WorkFlow.png)

## 不透明渲染
![](https://github.com/yqlizeao/UnitySoftRender/blob/master/OpaqueCapture.png)

## 透明渲染
![](https://github.com/yqlizeao/UnitySoftRender/blob/master/TranslucentCapture.png)
