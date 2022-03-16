---
title: 《GPU Gem3》第十二章笔记 - AO | Notes for GPU Gems 3 Chapter 12 - High-Quality Ambient Occlusion
date: 2022-03-16 20:47:02
categories:
- programming
- reading notes
tags:
- CG
- render
- GPU Gems 3
toc: true
---

# 早期算法

最开始的 AO 技术在多边形的顶点上使用 disk 近似相邻的多边形，接着将顶点周围的各个 disk 贡献的阴影累积到顶点上以近似 AO 。但这一做法忽略了可见性：被其他 disk 遮挡的 disk 不应该贡献 AO 。其中一种解决方法是以顶点的 occlusion 近似 visibility ，并迭代多次以使结果收敛。

这种算法的最大问题在于它 $O(n^2)$ 的时间复杂度在实践中很难实现。Bunnell 等[2005]根据远处的 disk 的贡献较小而可以使用一个聚合体近似的观察设计了 disk 的层次树状结构，从顶至下近似的精度越来越高。这有效地将复杂度降低至 $O(n\log n)$ ，进而可以实时地完成对可变形物体的 AO 计算。但这种方法也并非没有问题。

这种原始的算法可以较好地应用于拥有平滑的顶点 AO 过渡的几何体，以便可以在三角形上插值 AO 的影响，但高质量的渲染需要更加细粒度且更加复杂、不连续的 AO 计算。它无法模拟高频的接触阴影，会造成插值错误，还可能引入更多的 artifacts :

<center style="margin-bottom: 10px"><img src="12-1.png" style="max-width: 16vw; margin: 10px 0"/></center>

<center style="margin-bottom: 10px"><img src="12-2.png" style="max-width: 16vw; margin: 10px 0"/></center>

## Disk - Shaped Artifacts

上图中的 bunny 背部出现了圆形的错误，这些错误被称为 Disk - Shaped Artifacts ，它会导致画面上出现明显的圆形的亮暗分界线。这主要是在遍历树状结构时的离散估计操作导致的。

## 高频的挤压 Artifacts

上图右侧的模型上出现了很多高频的挤压阴影。当估计 disk 距离着色位置很近时，其贡献的阴影权重会大幅上升，导致其效果过大而产生此类阴影。

# 一种鲁棒的算法

本节中介绍了一种鲁棒的解决方法，它在基础算法结构之上做出了几个关键改变以提高效果。

## 平滑不连续位置

<center style="margin-bottom: 10px"><img src="12-3.png" style="max-height: 40vh; margin: 10px 0"/></center>

由于无法保证对父节点的影响一定等效于对子节点计算影响而引入的不连续分界面可以通过强制平滑转换而解决。通过在分界面边缘引入一段过渡区域，在区域内同时计算父子节点的影响，并使用距离插值从而获得平滑的边界过渡，其代码如下：

```glsl
// Compute shadow contribution from the current disk.    
float contribution = . . . 
// Stop or descend?    
if(d2 < tooClose * (1.0 + r)) {   
    // Remember the parent's contribution.   
    parentContribution = contribution;   
    // Remember the parent's area.   
    parentArea = area;   
    // Compute parent's weight: a simple linear blend.   
    parentWeight = (d2 – (1.0 – r) * tooClose)/(2.0 * r * tooClose);   
    // Traverse deeper into hierarchy.   
    . . . 
} else {   
    // Compute the children's weight.   
    childrenWeight = 1.0 – parentWeight;          
    // Blend contribution:   
    // Parent's contribution is modulated by the ratio of the child's area to its own.       
    occlusion += childrenWeight * contribution;   
    occlusion += parentWeight * parentContribution * (area/parentArea); 
}
```

## 去除挤压并增加细节

原始的算法使用 disk 表示顶点附近的几何结构，但这一做法在顶点周围的误差会非常大，而对于大曲率的位置，其结果就是巨大的阴影值了。为了解决这一问题，可以将 disk 的位置移动到面片的中点，同时在计算最细粒度的结构时直接使用解析解而避免这一情况。

传统的全局光照技术早已对求解遮挡率的解析解有了研究：一个未被遮挡的几何体 $A$ 对于法线为 $n$ 的点 $p$ 的遮挡率为：

$$
F_{pA} = {1 \over 2\pi}\sum_i n\cdot{e_i \times v_i \over |e_i \times v_i|}
$$

<center style="margin-bottom: 10px"><img src="12-4.png" style="max-height: 40vh; margin: 10px 0"/></center>

在实际使用时，还需要使用由目标点和法线构成的切平面裁剪出可视的部分进行计算，最终的结果往往是一个 quad ：

<center style="margin-bottom: 10px"><img src="12-5.png" style="max-height: 40vh; margin: 10px 0"/></center>

```glsl
void visibleQuad(float3 p,
                 float3 n, 
                 float3 v0, 
                 float3 v1, 
                 float3 v2, 
             out float3 q0, 
             out float3 q1, 
             out float3 q2, 
             out float3 q3) {   
    const float epsilon = 1e-6;   
    float d = dot(n, p);   
    // Compute the signed distances from the vertices to the plane.    
    float sd[3]; sd[0] = dot(n, v0) – d;   
    if(abs(sd[0]) <= epsilon) sd[0] = 0.0;   
    sd[1] = dot(n, v1) – d;   
    if(abs(sd[1]) <= epsilon) sd[1] = 0.0;   
    sd[2] = dot(n, v2) – d;   
    if(abs(sd[2]) <= epsilon) sd[2] = 0.0;   
    // Determine case.    
    if(sd[0] > 0.0) {     
        if(sd[1] > 0.0) {       
            if(sd[2] < 0.0) {         
                // v0, v1 above, v2 under         
                q0 = v0;         
                q1 = v1;         
                // Evaluate ray-plane equations:         
                q2 = v1 + (sd[1]/(sd[1] - sd[2])) * (v2 - v1);         
                q3 = v0 + (sd[0]/(sd[0] - sd[2])) * (v2 - v0);       
            } else {         
                // v0, v1, v2 all above         
                q0 = v0;         
                q1 = v1;         
                q2 = v2;         
                q3 = q3;       
            }     
        }   
    }   
    // Other cases similarly   . . .
}
```

# 结果对比

## 渲染结果

<center style="margin-bottom: 10px"><img src="12-6.png" style="max-width: 16vw; margin: 10px 0"/></center>

<center style="margin-bottom: 10px"><img src="12-7.png" style="max-width: 16vw; margin: 10px 0"/></center>

可以看出，对于密集且平滑的 bunny 模型，旧有方法表现尚可，但对于具有很大的细节差异的汽车模型而言结果就不那么好了。

## 性能表现

上述两个场景的参数如下：

|       | n_triangles | n_fragments | n_disks |
| ----- | ----------- | ----------- | ------- |
| bunny | 69,451      | 381,046     | 138,901 |
| car   | 29,304      | 395,613     | 58,607  |

性能如下：

<center style="margin-bottom: 10px"><img src="12-8.png" style="max-height: 40vh; margin: 10px 0"/></center>

在每个像素上使用鲁棒性算法的效率和在顶点上使用原有算法的性能相近，约比在像素上使用原有算法慢一倍出头。

# 注意事项

由于本算法本身仍然是一个近似算法，且无法完全地去除所有的不连续性，本文提出了数种方法辅助得到需要的艺术效果。

## 强制收敛

这种算法在估计 disk 的贡献时会使用一种迭代式的方法，但它在实践中并不一定会收敛，例如建筑场景往往会在两种过亮和过暗的场景之间反复切换。为了解决这一问题，本算法会在前数次迭代后将 AO 值限制为使用前两次迭代中较小的那个，或者在其中之间进行插值。代码和效果如下：

```glsl
float o0 = texture2DRect(occlusion0, diskLocation).x; 
float o1 = texture2DRect(occlusion1, diskLocation).x; 
float m = min(o0, o1); 
float M = max(o0, o1); 
// weighted blend 
occlusion = minScale * m + maxScale * M;
```

<center style="margin-bottom: 10px"><img src="12-9.png" style="max-height: 40vh; margin: 10px 0"/></center>

## 可调参数

我们注意到，本算法常常倾向于过度估计 AO 的程度。我们需要一个函数以控制 AO 在不同距离处的衰减，让近处的投影物可以造成高对比度的影响，而远处的投影物又不至于完全遮蔽光源。此处引入了两个可调参数：

### 距离衰减

这个参数控制了随距离变化各类 disk 对 AO 贡献值大小的衰减：

<center style="margin-bottom: 10px"><img src="12-10.png" style="max-height: 40vh; margin: 10px 0"/></center>

```glsl
// Compute the occlusion contribution from an element 
contribution = solidAngle(. . .); 
// Attenuate by distance 
contribution /= (1.0 + distanceAttenuation * e2);
```

### 三角形衰减

这个参数控制了在最细粒度下附近的三角形带来的遮蔽衰减：

```glsl
// Get the triangle's occlusion.    
float elementOcclusion = . . . 
// Compute the point-to-triangle form factor. 
contribution = computeFormFactor(. . .); 
// Modulate by its occlusion raised to a power. 
contribution *= pow(elementOcclusion, triangleAttenuation);
```

更小的参数可以强化细节三角形的影响，而更大的参数则会减弱大型三角形的影响，以防它们实际上被遮挡住了。

# Future Work

本节提出了一种稳定的 GPU 加速算法以计算高质量的环境光遮蔽效果。更进一步地，这种算法并不只能用于环境光遮蔽的计算中，实际上他可以被用于计算间接光照，如次表面散射可以使用同样的层次结构加速计算。简单地修改代码和 disk 中储存的值即可得到计算多次散射的间接光照的方法：

<center style="margin-bottom: 10px"><img src="12-11.png" style="max-height: 40vh; margin: 10px 0"/></center>

```glsl
float3 multipleScattering(float d2, 
                          float3 zr,     // Scattering parameters 
                          float3 zv,     // defined in    
                          float3 sig_tr) // Jensen and Buhler 2002 
{   
    float3 r2  = float3(d2, d2, d2);   
    float3 dr1 = rsqrt(r2 + (zr * zr));   
    float3 dv1 = rsqrt(r2 + (zv * zv));   
    float3 C1  = zr * (sig_tr + dr1);   
    float3 C2  = zv * (sig_tr + dv1);   
    float3 dL  = C1 * exp(-sig_tr/dr1) * dr1 * dr1;          
    dL += C2 * exp(-sig_tr/dv1) * dv1 * dv1;   
    return dL; 
}
```