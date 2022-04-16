---
title: 《GPU Gem3》第十三章笔记 - 使用后处理的体积光散射 | Notes for GPU Gems 3 Chapter 13 - Volumetric Light Scattering as a Post-Process
date: 2022-04-06 19:36:31
tags:
---

本章提出了一种使用后处理方法的体积光散射效果。

# 缝隙光

当空气中具有了足够浓度得到气溶胶后，遮光物会投影下体积阴影，看起来像是有光线从光源发出一样。对这种效果的渲染最开始在离线领域中可以使用修改过的 SV 方法实现，接着应用于实时渲染中，然而这些方法均会受到实际采样率的影响。另一种实时的方法是使用多边形体积和 FBO blending 和深度剔除，或者厚度累加等方法计算光线。本文中提出了一种不需要预处理的逐像素后处理方法以实现高细节的动态光线投影。

# 体积散射

为了计算体积散射的效果，我们需要计算光源到像素位置的散射以及该位置是否被遮挡。其中体积散射对于太阳光线的解析模型如下：

$$
L(s, \theta)=L_{0} e^{-\beta_{ex} s}+\frac{1}{\beta_{\mathrm{ex}}} E_{\text {sun }} \beta_{\mathrm{sc}}(\theta)\left(1-e^{-\beta_{ex} s}\right)
$$

式中的 $s$ 为在介质中穿过的距离，$\theta$ 是光线的夹角大小，$E_{sun}$ 是灯光发出的能量，$\beta_{ex}$ 是介质的消光常数，表示了吸收和外散射的效果，$\beta_{sc}$ 是角散射项。其中第一项代表了来自实际视点的光线的消光后的能量，第二项则描述了从其它方向散射到本方向的能量。

<center style="margin-bottom: 10px"><img src="Untitled.png" style="max-height: 25vh; margin: 10px 0"/></center>

接着考虑实际空间中的点 $\phi$ ，此时可以通过引入一个遮蔽项 $D(\phi)$ 描述能量的传输比例。

$$
L(s,\theta,\phi) = (1 - D(\phi))L(s,\theta)
$$

这一近似在当光源远比遮挡物的出射光亮时较为有效。虽然在屏幕空间中我们无法得到遮蔽项的解析解，但可以通过沿着屏幕空间中的光路进行采样来得到其近似解。因此有：

$$
L(s, \theta, \phi)=\sum_{i=0}^{n} \frac{L\left(s_{i}, \theta_{i}\right)}{n}
$$

进一步地，在上式中引入控制参数：

$$
L(s, \theta, \phi)=\text { exposure } \times \sum_{i=0}^{n} \text { decay }{ }^{i} \times \text { weight } \times \frac{L\left(s_{i}, \theta_{i}\right)}{n}
$$

其中的 exposure 参数用于大范围地控制全局的亮度，weight 参数则用于细调亮度，一个指数衰减参数 decay 让光线可以自然地衰减。由于样本是直接从场景中获取的，对透明物体的处理天然地存在于流程中而无需额外计算。而多光源的情况也可以通过叠加数次场景采样 pass 而实现。

对于每个光源 $s$ 和每个像素位置 $\phi$ ，会在光路上放置固定个采样点，此时引入了另一个参数 density 用于控制采样的距离占总长度的比例。这一值可以被用于在控制采样数的同时得到足够不走样的图像。

我们可以通过降采样原图来减少采样的次数。

# 后处理 Pixel Shader

本方法的核心就是下述的 Pixel Shader 。给定一张原始图片，会在各个像素上根据 density 生成到光源的屏幕位置的采样，这些采样会被 decay 和 weight 参数进行缩放作为效果的参数。而最终结果会被 exposure 参数调整亮度。

```glsl
float4 main(float2 texCoord : TEXCOORD0) : COLOR0 
{   
		// Calculate vector from pixel to light source in screen space.    
		half2 deltaTexCoord = (texCoord - ScreenLightPos.xy);   
		// Divide by number of samples and scale by control factor.   
		deltaTexCoord *= 1.0f / NUM_SAMPLES * Density;   
		// Store initial sample.    
		half3 color = tex2D(frameSampler, texCoord);   
		// Set up illumination decay factor.    
		half illuminationDecay = 1.0f;   
		// Evaluate summation from Equation 3 NUM_SAMPLES iterations.    
		for (int i = 0; i < NUM_SAMPLES; i++)   
		{     
				// Step sample location along ray.     
				texCoord -= deltaTexCoord;     
				// Retrieve sample at new location.    
				half3 sample = tex2D(frameSampler, texCoord);     
				// Apply sample attenuation scale/decay factors.     
				sample *= illuminationDecay * Weight;     
				// Accumulate combined color.     
				color += sample;     
				// Update exponential decay factor.     
				illuminationDecay *= Decay;   
		}   
		// Output final color with a further scale control factor.    
		return float4( color * Exposure, 1); 
}
```

# 屏幕空间遮蔽方法

在屏幕空间进行采样会受到物体的表面材质的影响而出现错误的条纹。下文提出了一系列方法以解决这一问题：

## 遮蔽 Pre-Pass 方法

这一方法通过实现将遮蔽物渲染为黑色，并在这一 FBO 上采样光线，最后将结果叠加到原图上从而得到想要的效果。下图 a 是无预处理的方法的效果，bcd 则展示了预处理的流程和结果。这一步预处理可以通过其他常规 pass 得到。

<center style="margin-bottom: 10px"><img src="Untitled%201.png" style="max-height: 40vh; margin: 10px 0"/></center>

## 遮蔽模板剔除法

模板测试或 alpha 缓冲同样可以被用于实现类似的效果，天空区域在渲染的同时会额外设置一个模板 bit ，而遮挡物则没有。在应用后处理时读取本缓存就可以剔除掉遮挡物样本的影响了。

## 遮蔽对比方法

这一问题还可以通过降低材质的对比度来解决

# 额外说明

虽然这一方法可以得到优秀的结果，但它也并非毫无问题，例如背景物体投下的光束会渲染在前景物体的前方，而事实上它应该因为被前景物体所遮挡而变暗。

另一个问题在于当遮蔽物和光源在图像边界之外的时候，采样的结果会因为样本的越界而产生闪烁问题。常用的解决方法是在边界外侧额外渲染一部分遮挡物以增加可选用的样本域。

最终，当观察方向和光线方向接近垂直时，光源位置会趋近于无穷大，从而导致非常大的采样间隔。解决方法包括了将光源的屏幕空间坐标限制在有限的区域内，或是根据光线的角度减弱效果直至消失等。

# 拓展

一种优化方法是可以通过缩小采样的分辨率而降低对材质带宽的要求，并可以使用随机的采样来减少各类 artifacts 。齐次，本文中实现的是一种单 pass 的方法，而实际上各个像素完全可以在多个 pass 中利用光路上其它像素的采样结果来进一步减少采样的次数。最终，控制光幕的亮度和避免过饱和问题需要大量对消光系数的调整，通过使用解析方法找到图像中的平均和最大最小亮度值，进而对图像应用一个自适应的颜色映射可以有效避免图像的过亮或过暗。