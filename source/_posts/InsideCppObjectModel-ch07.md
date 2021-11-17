---
title: 《深度探索C++对象模型》第七章笔记 | Notes for Inside Cpp Object Model Chapter 07
date: 2021-11-17 17:03:18
categories:
- programming
- reading notes
tags:
- C++
- OOP
- 深度探索C++对象模型
toc: true
---

# Ch07 站在对象模型的尖端

Date: November 16, 2021 → November 17, 2021

本章中讨论了三个著名的 C++ 语言扩充性质：template、exception handling (EH) 和 runtime type identification (RTTI)

# 7.1 Template

C++ 程序设计的风格自 cfront 3.0 中引入 template 开始就发生了深远的变化，这一功能本来是对 container classes 的一项支持，但它如今已经成为了通用程序设计的基础、用于属性混合、互斥机制等多处的参数化技术中。

## Template 的实例化

在 C++ 中，任何一个 class object 的定义（不包括指针），无论是编译器内部使用的临时对象还是用户定义的对象都会导致 template class 的实例化。然而，template class 中的内容必须要与一个实例化的类型绑定，如下例：

```cpp
template <class T>
Class Sample
{
public:
		enum Status {stat1, stat2}
		static int val;
		void foo(T bar);
};
```

虽然该类中的 `Status` 枚举类和静态变量并不需要与任何类型参数绑定，但它仍然需要通过某个类型的实体才能访问，这将不得不产生多余的实体，即：

```cpp
Sample::Status e; // 错误的访问方法
Sample<int>::Status x; // 必须这样使用
Sample<float>::Status y; // 和上一行不同的实体
```

实例化的另一个特性是：只实例化需要使用的成员函数（虽然当前的编译器并不都遵循这项要求）。

对于不同的类型标识符，模板在实例化时可能会出现不同的结果。对于基础操作符，如一些系统中即使 int 和 long 是等价的，但 `Sample<int>::val` 和 `Sample<long>::val` 产生的是两个不同的实体。但如 `size_t` 和 `unsigned long long` 这种使用 `typedef` 或者宏定义别名的类型却只会产生一种实体。但这只是一种行业默认的做法，并未在 C++ 标准中有明确的强制规定。

## Template 的错误信息

考虑一个具有很多错得离谱的类型声明：

```cpp
template <class T>
class Sample
{
public$
    Sample(T t = 1024) : _t(t)
    {
        if (tt != t) throw ex ex;
    }
private:
    T tt;
}
```

这里面隐藏了许多需要在模板类实例化后才能检测出的问题，包括了：

- L5 中是否可以使用 1024 赋值给一个 T 类型的对象
- L7 中 T 类型是否定义了 `operator!=`

这些问题在 nontemplate class 的声明中可以很轻易地在编译期就被找到。但对于 template class ，他们必须在每个实例化发生之后才能被检测出来。如 `Sample<int>` 中上两行的写法是合法的，而在 `Sample<int *>` 中则是非法的。

在处理 template 的声明时，只有语汇（lexing）错误和解析（parsing）错误会被找到，而类型检查需要在每一个实例化发生时才会发生。对于上一节例中的函数声明，即使下面这个写法错得离谱，只要没有被实例化（对于函数而言，直到函数被使用时才被实例化），那么它在各个编译器的编译过程中均不会报错。

```cpp
template <class T> void T1<T>::foo(T bar)
{
    this->xxx = 0; // xxx 不是上述类中的 data member
}
```

## Template 中的名称决议（Name Resolution）

在模板中的函数类型确认同样也被推迟到了实例化发生的地点。编译器会从实例化发生时的语境中选择最适合模板类中的函数的具名函数链接到对于语句上，而并非只考虑在 template 定义时的语境。

> 这一部分里面的内容和我的试验结果有差异，在 clang 和 msvc 上与书中结果相同，但在 gcc 上不同。测试中使用的代码如下：
>
>
> ```cpp
> /* main.cpp */
> #include "common.h"
> using namespace std;
> void foo(float v) { cout << "float foo" << endl; }
> Sample<float> obj;
> int main(int argc, char const *argv[])
> {
>     obj.intCall();
>     obj.typeCall();
>     return 0;
> }
> 
> /* common.h */
> #pragma once
> #include <iostream>
> using namespace std;
> void foo(int v) { cout << "int foo" << endl; }
> template <class T> class Sample
> {
> public:
>     void intCall() { foo(v1); }
>     T typeCall() { foo(v2); }
> private:
>     int v1;
>     T v2;
> };
> ```
>
> 上述代码在 clang 和 msvc 会分别调用 int foo 和 float foo，而在 gcc 上却只调用 int foo

## Member Functions 的实例化

为了实现对模板函数的支持，编译器的设计者需要考虑以下三个主要问题：

1. 编译器如何找出函数的定义？

   其中一种解决方法是要求一个文件命名规则，如在 Example.h 中声明的模板函数的函数体必须在对应的 Example.c 或 Example.cpp 中定义。

2. 编译器如何只实例化程序中用到的函数？

   目前的实现方法主要是两种，一是无视这一需求实例化所有可能用到的函数，另一种是在编译期间进行仿真链接操作以找到可能用到的函数。

3. 编译器如何阻止同一个 member function 在多个 .o 文件中被实例化？

   这一问题的解决方法和上一个类似，要么忽略它转而在链接过程中完成去重的过程，要么在编译期间利用仿真链接操作去重。

上述这些问题与相应的解决方案都会造成编译时间的大量增加。为了解决这一问题，Edison Design Group 开发了一套第二代的 directed-instantiation 机制，其主要过程为：

1. 在编译期间不会产生任何 template 实体，只将相关信息储存在 object files 中
2. 在连接前使用 prelinker 检查 object files 以寻找 template 实体的相互参考和定义
3. 对于每个有 template 实体参考但是找不到定义的情况，将必要的程序实例化操作指定给对应的文件，并注册在 .ii 文件中
4. prelinker 重新执行编译器以重新编译每一个 .ii 文件发生了变化的文件
5. 重复上述过程知道所有必要的实例化操作完成，最后执行链接器产生可执行文件

但这也并不能完美地解决问题，在实现层面上，这似乎是 template 实例化所带来的对自动化和效率的一大瓶颈。当程序十分复杂且巨大的话，在个别的位置手动完成预先实例化的操作仍然是唯一的有效率的方法。

# 7.2 异常处理（Error Handling）

为了支持 EH ，编译器的主要工作就是找到 catch 语句以处理被抛出的 exception 。这需要跟踪程序堆栈中每个函数的作用域、并提供查询 exception object 类型的方法。最后，编译器还需要某种机制以管理被抛出的 exception object 。

## Exception Handling 的结构

C++ 中的 exception handling 主要有三个语汇组件构成：

1. 一个 try 区段以包裹一系列语句，这些语句可能会抛出异常
2. 一个 throw 子句，它在程序中的某个位置发出 exception
3. 至少一个 catch 子句以处理不同类型的 exception

当一个 exception 被抛出时，在找到对应的 catch 子句前，堆栈中的每个函数调用都会被 pop 出去，并在离开前调用所有 local objects 的析构函数。如果没有找到符合的 catch 语句，默认的 terminate() 处理方法就会被调用结束程序。

```cpp
void example()
{
    foo();
    SomeClass object = 0;
    bar();
    return;
}
```

EH 的引入需要程序记录更多的执行期语句，如上述函数片段中的两次函数调用的位置具有了不同的执行期语义：后者在退出前需要调用 object 的析构函数。这种信息的记录通常使用一个储存了需要析构的对象的链表实现。

EH 带来的另一问题是，如果 exception 在某些关键操作（如共享内存的上锁与解锁）之间发生，可能会造成资源的错误配置。这种时候一个最明确的方式就是对资源申请后直到释放前的使用额外的 try catch 保护。或者也可以将资源的申请和释放包裹在 class 的 constructor 和 deconstructor 中，这样就可以让程序在退出函数时自动释放已经申请的资源了。

## Exception Handling 的支持

当一个 exception 发生时，编译系统需要完成以下事情：

1. 检查发生 throw 的函数
2. 确定 throw 是否发生在 try 中，这通常可以通过比较当前的程序计数器（pc）和编译时得到的 try 区段表得到
3. 如果在 try 中，则需要将 exception type 和每个 catch 比对类型，如果比对吻合则将流程控制交给 catch 字句中
4. 如果不在 try 中或没有符合的 catch 语句，则：
   1. 析构所有的 active local objects
   2. 从堆栈中将当前函数 unwind 出取
   3. 进入堆栈中的下一个函数中，重复以上 2 - 4 步

当一个 exception 被抛出时，一个 exception object 会被产生并放置在对应的数据堆栈中。这个对象的地址和其类型描述器（或提供类型描述器的函数）会被传给 catch 字句。

在 exeption handling 的 catch 部分中依旧支持虚拟机制。在以下的例子中的表现和将一个 object 传入与 catch 对应的函数非常类似：

```cpp
try
{ throw Derived(); }
catch (Base e)
{
    // 这个区段中使用的 e 发生了裁切，丢失了派生类的内容
    throw; // 在不指定 exception 抛出时，实际抛出的是原始的派生类型，之前的修改都会被抛弃
}
// 或者可以使用引用
catch (Base &e)
{
    // 在这个区段中虚拟机制在起作用
}
```

## Exception Handling 的代价

### 程序文件大小的差异

| 编译器    | 无EH  | 有EH  | 增加的百分比 |
| --------- | ----- | ----- | ------------ |
| Borland   | 86822 | 89510 | 3%           |
| Microsoft | 60146 | 67071 | 13%          |
| Symantec  | 69786 | 74826 | 8%           |

### 程序运行时间的差异

| 编译器    | 无EH | 有EH | 增加的百分比 |
| --------- | ---- | ---- | ------------ |
| Borland   | 78s  | 83s  | 6%           |
| Microsoft | 83s  | 87s  | 5%           |
| Symantec  | 94s  | 96s  | 4%           |

# 7.3 执行期类型识别（RTTI）

由于真正的 exception 是在执行期被处理的，其 object 必须带有自己的类型信息。Runtime Type Identification (RTTI) 便是支持这一特性所获得的副产物。这一功能用于在程序中实现安全的 downcast 或 dynamic cast 。

## 类型安全的 downcast

只有在类型可以被适当转型的情况下才能执行 downcast 。一个类型安全的 downcast 必须在执行期查看指针或引用所指的对象的真正类型，这需要额外的空间储存类型信息和额外的时间决定执行期的类型。

C++ 的 RTTI 机制提供一个安全的 downcast 操作，但这种机制只对使用了继承和动态绑定的类型有效。为了让编译器分辨这些类型，编译器通常使用的策略是经由声明一个或多个 virtual function 以区分 class 声明。更进一步的，通常会直接将类型对应的 RTTI object 的指针放入 vtbl 之中。

## 类型安全的 dynamic cast

`dynamic_cast` 运算符可以在执行期决定真正的类型。如果这个 downcast 是安全的，运算符就会返回被转型后的指针，否则就返回空指针。这通常是利用上述在 vtbl 中插入的 type_info 类型描述器完成的。

## Reference 和 Pointer 的区别

当 `dynamic_cast` 应用与指针上时，它可以返回一个 0 值以表示这个转换是不安全的，但一个引用并不能指向一个空对象。因此，当 `dynamic_cast` 被施加于一个引用上时，会发生以下的事情：

- 如果 downcast 是安全的，它会被正常地执行
- 如果 downcast 是不安全的，那么它会抛出一个 bad_cast exception

## typeid 运算符

无论是静态地对实体或类型使用，还是在运行期动态地对指针或引用使用 `typeid` 运算符就可以得到对应类型的 `type_info` 的一个 const reference。

对于每一个描述块，编译器需要提供的最小信息包括了比较函数与 class 的真实名称。C++ Standard 中定义的类型如下：

```cpp
class type_info {
public:
	  virtual ~type_info();
	  constexpr bool operator==(const type_info& rhs) const noexcept;
	  bool before(const type_info& rhs) const noexcept;
	  size_t hash_code() const noexcept;
	  const char* name() const noexcept;
	
	  type_info(const type_info&) = delete;                   // cannot be copied
	  type_info& operator=(const type_info&) = delete;        // cannot be copied
};
```

RTTI 不仅仅适用于用户定义的类型，它还适用于一些基础的类型。你同样可以通过 `typeid(int)` 取得 `int` 类型的描述块。

# 7.4 效率和弹性

传统的 C++ 对象模型提供了较为高效的执行期支持。这种效率加上与 C 的兼容性保证了 C++ 的广泛接受度。然而，在动态函数库（Dynamic Shared Libraries）、共享内存（Shared Memory）以及分布式对象（Distributed Object）方面，这个对象模型的弹性还是不够。

## 动态函数库（Dynamic Shared Libraries）

理想上，一个动态链接的 shared library 的调用应该是透明的，已经链入的动态库的版本改变不应该对旧有的应用程序产生影响。然而在 C++ 的对象布局中，如果新版库中的 class object 的布局有所改变，那么对应的 member 偏移量也会变化，最后导致整个应用程序需要重新编译。

## 共享内存（Shared Memory）

当一个共享库被加载时，它在内存中的位置由 runtime linker 决定，一般而言与执行中的进程无关。然而当它之中包含了一个需要支持在共享内存中的虚函数的 class object 时，对与其它的进程而言，除非这个动态函数库被放置于完全相同的内存位置上，要不然就会产生严重的虚函数调用问题。这是因为虚函数在 vtbl 中的位置以及被写死了。

这一切问题的出现都主要是因为 C++ 对象模型对高效性和 C 兼容性的坚持带来的的包袱。但这也正是 C++ 广泛的适用性的来源。