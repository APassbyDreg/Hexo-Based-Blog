---
title: UE 的宏定义 ShaderParameters
date: 2024-12-28 22:50:59
categories:
- programming
- gamedev
tags:
- UnrealEngine
toc: true
---

之前一直很好奇 UE 是怎么用宏定义搞出对应 member 名称、类型和值的对应方法的，就花了点时间研究了一下。

# Example

声明：

```c++
BEGIN_SHADER_PARAMETER_STRUCT(FCubeShaderParameters, )
	SHADER_PARAMETER_RDG_TEXTURE_SRV(TextureCube, SourceCubemapTexture)
	SHADER_PARAMETER_SAMPLER(SamplerState, SourceCubemapSampler)
	SHADER_PARAMETER(FVector2f, SvPositionToUVScale)
	SHADER_PARAMETER(int32, CubeFace)
	SHADER_PARAMETER(int32, MipIndex)
	SHADER_PARAMETER(int32, NumMips)
	RENDER_TARGET_BINDING_SLOTS()
END_SHADER_PARAMETER_STRUCT()
```

使用：

```c++
auto* PassParameters = GraphBuilder.AllocParameters<FCubeFilterPS::FParameters>();
PassParameters->SourceCubemapTexture = GraphBuilder.CreateSRV(FRDGTextureSRVDesc::Create(SourceTexture));
PassParameters->SourceCubemapSampler = TStaticSamplerState<SF_Trilinear, AM_Clamp, AM_Clamp, AM_Clamp>::GetRHI();
PassParameters->CubeFace = CubeFace;
PassParameters->MipIndex = MipIndex;
PassParameters->NumMips = NumMips;
PassParameters->SvPositionToUVScale = FVector2f(1.0f / MipSize, 1.0f / MipSize);
PassParameters->RenderTargets[0] = FRenderTargetBinding(FilteredCubemapTexture, ERenderTargetLoadAction::ENoAction, MipIndex, CubeFace);
```

# 反射的实现

把这玩意展开来看看，大概能够得到这么一个东西：

```c++
// BEGIN_SHADER_PARAMETER_STRUCT
class ShaderParameters {
  struct zzFirstMemberId {};
  typedef void *zzFuncPtr;
  typedef zzFuncPtr (*zzMemberFunc)(zzFirstMemberId,
                                    TArray<FShaderParametersMetadata::FMember> *);
  static zzFuncPtr zzAppendMemberGetPrev(zzFirstMemberId,
                                         TArray<FShaderParametersMetadata::FMember> *) {
    return nullptr;
  }
// SHADER_PARAMETER TypeInfo MyVariable01
  typedef zzFirstMemberId zzMemberIdMyVariable01;
  TypeInfo::TAlignedType MyVariable01;
  struct zzNextMemberIdMyVariable01 {};
  static zzFuncPtr
  zzAppendMemberGetPrev(zzNextMemberIdMyVariable01,
                        TArray<FShaderParametersMetadata::FMember> *Members) {
    // do something to members
    zzFuncPtr (*PrevFunc)(zzMemberIdMyVariable01,
                          TArray<FShaderParametersMetadata::FMember> *);
    PrevFunc = zzAppendMemberGetPrev;
    return (zzFuncPtr)PrevFunc;
  }
// SHADER_PARAMETER TypeInfo MyVariable02
  typedef zzNextMemberIdMyVariable01 zzMemberIdMyVariable02;
  TypeInfo::TAlignedType MyVariable02;
  struct zzNextMemberIdMyVariable02 {};
  static zzFuncPtr
  zzAppendMemberGetPrev(zzNextMemberIdMyVariable02,
                        TArray<FShaderParametersMetadata::FMember> *Members) {
    // do something to members
    zzFuncPtr (*PrevFunc)(zzMemberIdMyVariable02,
                          TArray<FShaderParametersMetadata::FMember> *);
    PrevFunc = zzAppendMemberGetPrev;
    return (zzFuncPtr)PrevFunc;
  }
  typedef zzNextMemberIdMyVariable02 zzLastMemberId;

// END_SHADER_PARAMETER_STRUCT
public:
  static TArray<FShaderParametersMetadata::FMember> zzGetMembers() {
    TArray<FShaderParametersMetadata::FMember> Members;
    zzFuncPtr (*LastFunc)(zzLastMemberId,
                          TArray<FShaderParametersMetadata::FMember> *);
    LastFunc = zzAppendMemberGetPrev;
    zzFuncPtr Ptr = (zzFuncPtr)LastFunc;
    do {
      Ptr = reinterpret_cast<zzMemberFunc>(Ptr)(zzFirstMemberId(), &Members);
    } while (Ptr);
    Algo::Reverse(Members);
    return Members;
  }
};
```

members 的反射信息主要由 `zzGetMembers` 函数完成，它会反复调用 `zzAppendMemberGetPrev` 从后往前地添加 member 到列表中，并返回前一个 member 的 `zzAppendMemberGetPrev` 函数地址，直到在最开头的位置返回一个空指针结束。最后，把列表翻转就可以得到从前往后定义的 members 顺序。

每一个 members 的 `zzAppendMemberGetPrev` 函数都由一个 `zzNextMemberId{MemberName}` 结构体作为第一个参数进行标识，在定义参数时会留下一个 `typedef zzNextMemberId{MemberName}` 为下一个 member 用于定位到上一个 `zzAppendMemberGetPrev` 函数的指针，直到结束时将标识符定义为 `zzLastMemberId` 用于为 `zzGetMembers` 函数提供调用入口。

# 类型的实现

每个 member 的类型需要从给定 `SHADER_PARAMETER` 的数据类型中提取出来，它使用了基于模板的 meta class 方法：

```c++
/** Template to transcode some meta data information for a type <TypeParameter> not specific to shader parameters API. */
template<typename TypeParameter>
struct TShaderParameterTypeInfo
{
	/** Defines what the type actually is. */
	static constexpr EUniformBufferBaseType BaseType = UBMT_INVALID;

	/** Defines the number rows and columns for vector or matrix based data typed. */
	static constexpr int32 NumRows = 1;
	static constexpr int32 NumColumns = 1;

	/** Defines the number of elements in an array fashion. 0 means this is not a TStaticArray,
	 * which therefor means there is 1 element.
	 */
	static constexpr int32 NumElements = 0;

	/** Defines the alignment of the elements in bytes. */
	static constexpr int32 Alignment = alignof(TypeParameter);

	/** Defines whether this element is stored in constant buffer or not.
	 * This informations is usefull to ensure at compile time everything in the
	 * structure get defined at the end of the structure, to reduce as much as possible
	 * the size of the constant buffer.
	 */
	static constexpr bool bIsStoredInConstantBuffer = true;

	/** Type that is actually alligned. */
	using TAlignedType = TypeParameter;

	/** Type that has a multiple of 4 components. */
	using TInstancedType = TypeParameter;

	static const FShaderParametersMetadata* GetStructMetadata() { return TypeParameter::FTypeInfo::GetStructMetadata(); }
};

template<>
struct TShaderParameterTypeInfo<int32>
{
	static constexpr EUniformBufferBaseType BaseType = UBMT_INT32;
	static constexpr int32 NumRows = 1;
	static constexpr int32 NumColumns = 1;
	static constexpr int32 NumElements = 0;
	static constexpr int32 Alignment = 4;
	static constexpr bool bIsStoredInConstantBuffer = true;

	using TAlignedType = TAlignedTypedef<int32, Alignment>::Type;
	using TInstancedType = FIntVector4;

	static const FShaderParametersMetadata* GetStructMetadata() { return nullptr; }
};
// 下面是一系列特化的 meta 类型
```

输入的参数类型（如 `int32, FVector2f` 等）会被宏转换为 `TShaderParameterTypeInfo<MemberType>` 作为 `TypeInfo` 以获取 meta 类型信息，用于定义具体的 member 变量和在前述反射里记录 member 的信息。

对于比较特殊的 Texture / SRV / UAV 等则会在宏定义展开期间直接转化为定义好的 meta class ，如 `FRHITexture*, FRHIShaderResourceView*` 等。