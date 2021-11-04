---
title: 《深度探索C++对象模型》第四章笔记 | Notes for Inside Cpp Object Model Chapter 04
date: 2021-11-01 21:52:35
categories:
- programming
- reading notes
tags:
- C++
- OOP
- 深度探索C++对象模型
toc: true
---



# 4.1 Member 的各种调用方式

## Nonstatic Member Functions

C++ 的设计准则就是让 nonstatic member functions 的开销和一般的函数完全相同，它不会带来任何额外负担，因为编译器内部会将该函数实体转化为对等的 nonmember 函数实体。转化的步骤在于：

1. 改写函数原型，增加一个额外的 `this` 指针，如果这个函数有 `const` 修饰符，则指针也需要有该修饰符
2. 将该函数重写成一个外部函数，并对函数的名称进行「mangling」处理，使它变成独一无二的词语
3. 改写每个这个函数的调用操作

对于函数名称的改写方式在各个编译器中并没有统一的方式，其中一种可能的做法是在函数名前面添加类名和、参数列表的信息

## Virtual Member Functions

一个通过多态调用的虚成员函数需要进行运行时确定调用的函数体，一般是从 vtbl 中对应的 slot 位置找到该函数对应的指针再进行函数调用操作。对于虚函数中对于其它虚函数的调用，由于在分析出虚函数调用来自的实体的时候就已经掌握了对应的类型信息，因此并不需要额外的运行时开销。

另一方面，直接从实体调用的虚成员函数也不需要从 vtbl 中取地址，它绑定的实体在编译期间即可确定。编译器会以对待普通 nonstatic member functions 的方式处理这些函数。

## Static Member Functions

在引入 static member functions 之前，C++ 要求所有 member functions 均需要经由对应的 class object 调用。而实际上只有当一个或多个 nonstatic data members 在函数中被使用的时候才需要一个 class object。当没有任何 nonstatic member 被使用时，该函数中的 `this` 也就没有了作用（但 C++ 不能识别这个情况）。

这样的话，若是一个 static member 被声明为 nonpublic ，它的存取就不得不通过一个 member function 来实现。在过去，可能会出现一些奇怪的代码：

```cpp
((ClassType *) 0)->get_object_cnt();
```

static member functions 便是用于解决这种问题。它的主要特性就是不含 `this` 指针，因此就拥有了一系列次要特性：

1. 不能直接存取 nonstatic members
2. 不能被声明为 `const, volatile, virtual`
3. 不需要经过 class object 调用

nonstatic 函数由于缺乏 `this` 指针，使它拥有了一些意想不到的好处：可以作为 callback 函数，或是应用在线程函数上。

# 4.2 Virtual Member Functions

本节中深入探究了 vtbl 模型。

## 多态的表示

为了支持 virtual function 机制，必须要能够对多态对象有某种执行期的类型判断方法。

第一种解决方案是直接把必要的信息加在指针上，使它包括了参考的对象地址以及其对象类型的某种编码或结构。这个方法带来了两个问题，一是程序即使不使用多态也会增加空间负担，另一方面它也不能与 C 程序之间链接兼容。

第二种方法即将必要信息放在对象本身中，但一个结构是否需要那些额外的信息则是一件非常难以判断的事情。这会造成类内存的膨胀以及丧失与 C 接口的兼容性。为了判断哪些类型需要什么信息，需要一个更好的规范以判断在何时需要放入哪些信息：

1. 信息的用途：在 C++ 中，多态表示了以一个基类的指针寻址出一个派生类的对象的意思。然而对于多态的使用，又有着不同的要求：
   
    ```cpp
    Base *ptr;
    // 这种使用方法可以通过对 virtual function call 的 resolve 完成
    ptr->function();
    // 这种使用方法需要 runtime type identification (RTTI) 的支持
    if (Derived *dptr = dynamic_cast<Derived>(ptr))
    {
    		dptr->member++;
    }
    ```
    
2. 信息的内容：为了正确的实现多态，运行期间需要直到的信息包括了指针指向的变量的真实类型，以及对应的实体的位置。由此需要一个 vtbl 的机制，在 class object 中安插指向 vptr 指向虚函数表，并给每个对应的函数指派一个固定的 vtbl 索引值。一个类中的 vtbl 的内容包括了：
    1. 这个类中定义的函数实体，它可能重载了一个 base class virtual function
    2. 继承自 base class 的没有被重写的函数实体
    3. 纯虚函数的实体（一般会统一指向一个异常函数如 `pure_virtual_function_called()` ）

## 派生的实现

当一个含有虚函数表的类型被作为基类单派生后，新的函数可能有以下几种可能：

1. 继承了基类的虚函数实体，对应的函数地址会被拷贝到派生类虚表的对应位置
2. 使用自己重载的函数实体，将新的函数地址覆盖虚表的对应位置
3. 增加新的虚函数，此时虚表的尺寸也会增加以存放新的函数地址

在单派生链中，每一个虚函数在重载前后都存在于虚表的同一个偏移位置处，这是非常直观且运行良好的，然而一旦引入了多继承和虚拟继承，对虚函数的支持就不那么好了。

## 多继承下的虚函数

在多重继承中支持虚函数的复杂度主要围绕在第二个以及后续的基类上，以及必须在执行期间调整 this 指针这一点。

在多重继承之下，每个 n 重继承的派生类都会有 n 个虚函数表（单一继承如上所述不会产生额外的虚函数表）。对于每一个虚函数表，派生对象中都会有对应的 vptr 一种可能的布局如下图所示：

<center><img src="vtbl.png" style="max-height: 50vh; margin: 10px 0;"/></center>

有三种情况，第二个或者后续的基类会影响对虚函数的支持，这在上图中用三个 * 号表示：

1. 通过一个指向第二个基类的指针调用派生类重载后的虚函数
2. 通过指向派生类的指针调用未被重载的第二个或之后的基类中的虚函数
3. 在 C++ 语言扩充性质下，允许一个虚函数的返回值发生变化，可能是 base type 也可能是 public derived type ：
   
    ```cpp
    Base2 *pb = new Derived;
    // 此时返回的是 Derived::clone() ，它重载了两个基类中的虚函数
    Base2 *_pb = pb->clone();
    ```
    

## 虚继承下的虚函数

当一个派生类从一个基类虚拟派生而来时，即使只是单一继承，其基类对象在派生类中都是未知的，需要在运行期决议并调整对应的 `this` 函数指针。

当一个 virtual base class 从另一个 virtual base class 中派生而来且二者都有 virtual functions 和 nonstatic data members 是，编译器对于它的支持将会变得极端复杂且诡异。最简单的方式就是不要再 virtual base class 中声明任何 nonstatic data member （和有实体的 nonstatic function）

# 4.3 函数的效率

使用类似以下的方法测试不同种类的函数的效率：

```cpp
// nonmember function 实例
void cross_product(const Point3d &pA, const Point3d &pB)
{
		Point3d pC;
		/* ... */
		return pC;
}

int main() 
{
		Point3d pA, pB;
		/* ... */
		for (int iters = 0; iters < 1e7; iters++)
		{
				// nonmember function 实例
				cross_product(pA, pB);
		}
}
```

<center><img src="func-efficiency.png" style="max-height: 50vh; margin: 10px 0;"/></center>

需要注意的地方有几个：

1. inline 函数不止能节省一般函数调用带来的额外负担，还能提供给程序额外的优化机会
2. 多重继承增加的时间消耗实际上源于函数中初始化返回值用的那个点的 constructor 处理了更多的虚拟函数相关的信息。

# 4.4 指向 Member Function 的指针

在上一章中曾经讲到，取一个类中 nonstatic data member 的地址实际上得到的是它在类内布局中的字节位置（有时需要加一）。它是一个不完整的值，必须绑定在某个 class object 的地址上才能被存取。一个指向 nonstatic member function 的指针的性质也与此相似，必须绑定于特定的 class object 的地址上（因为需要 `this` 指针），而 static member function 则不需要。

## 单继承下的情况

对于一个指向非虚函数的指针，得到的将会是它在内存中的直接地址。而对于指向虚函数的指针，得到的则是该函数在虚函数表中的索引值（有时需要加一）。但这会导致函数指针的二义性，当普通函数和虚函数具有同样的参数列表和返回值时，可能会出现内容的冲突。多重继承的引入提出了更加一般的实现方式的需要，顺势解决了这个问题。

## 多继承下的情况

为了能让指向成员函数的指针也可以支持多继承和虚拟继承，Stroustrup 中设计了如下的结构体记录函数指针信息：

```cpp
struct __mptr {
		int delta; // this 指针使用时需要的偏移值
		int index; // vtbl 中的函数位置索引，为 -1 时表示不指向 vtbl
		union {
				__ptrtofunc faddr; // 内存中的函数指针
				int v_offset;      // 多继承或者虚拟继承中的 vptr 位置
		}；
};
```

## 成员函数指针的效率

<center><img src="fptr-efficiency.png" style="max-height: 50vh; margin: 10px 0;"/></center>

# 4.5 Inline Functions

考虑一个点的加法运算：

```cpp
Point operator+(const Point &pt1, const Point &pt2)
{
		Point new_pt;
		new_pt._x = pt1._x + pt2._x;
		/* ... */
		return new_pt;
}
```

理论上，使用 inline 函数完成 get 和 set 能让系统变得更加干净：

```cpp
void Point::x(float x) { _x = x; }
float Point::x() { return _x; }

new_pt.x(pt1.x() + pt2.x());
```

inline 函数的引用可以将其后可能因为在继承体系中的位移等改变带来的 data members 的变化最小化，同时保持和原有写法一样高的效率。另一方面，这个加法函数也不需要声明为原函数的一个 friend 了。

然而实际上 C++ 中的 inline 关键字并不是强制要求，它只是一个请求。如果编译器认为该函数无法被合理的扩展（即执行成本不比普通函数调用低），他就不会将函数 inline 化。对于执行成本的估计，通常是用函数中计算 assignments、function calls、virtual function calls 等操作的加权次数统计得到的。

一般而言，处理一个 inline 函数有两个阶段：

1. 分析函数的定义。如果函数因为其复杂度或者构建问题被判断为不可以成为 inline ，它则会被转化为一个 static 函数，并在被编译的模块内产生对应的函数定义。
2. 真正的 inline 函数扩展操作发生在它被调用的时刻，着会带来参数求值和临时对象管理的问题。在扩展点上，编译器同样会判断这个调用是否不需要成为 inline 调用。需要注意的是，如果 inline 函数只有一个表达式，那么它第二个或者更加往后的调用方式就不会被扩展开。上述的第二种写法中的调用会被扩展为及其丑陋的代码：
   
    ```cpp
    new_pt.x= pt1._x + x__5PointFV(&pt2);
    ```
    

## inline 函数的形式参数管理

在 inline 扩展期间，每个形参都会被实际参数所替代。然而，并不能简单地直接将所有形参替换为传入的表达式，因为这可能导致对实际参数额外的求值操作。一般而言，这些可能导致副作用的实参均需要引入临时变量代替。

如：inline 函数调用 `func(1, foo(), bar() + 3);` 的后两项就会在编译期替换为临时变量。

## inline 函数的局部变量管理

一般而言，inline 函数中点每个局部变量都必须放在函数调用的一个封闭区间内，并拥有独一无二的名称。

当 inline 函数以分离的表达式的形式被扩展多次时，一般只需要一组局部变量，然而当它在单个表达式中多次使用并扩展时，每一次扩展都需要自己的局部变量。这些额外的局部变量和上一节中用来替换有副作用的实参的临时变量可能会导致大量临时性对象的产生，从而使程序的大小暴增。