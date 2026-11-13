#pragma once
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#define BYTE_ORDER LITTLE_ENDIAN
#define LWIP_PLATFORM_DIAG(x) do { printf x; } while(0)
#ifdef _MSC_VER
#define PACK_STRUCT_BEGIN __pragma(pack(push,1))
#define PACK_STRUCT_END __pragma(pack(pop))
#endif
#define MEM_ALIGNMENT 8
#define LWIP_PLATFORM_ASSERT(x) do { fprintf(stderr,"lwip assert: %s\n",x); abort(); } while(0)
#define LWIP_RAND() ((uint32_t)rand())
#define LWIP_NO_UNISTD_H 1
#define SSIZE_MAX INT_MAX
