#pragma once
#include <stdint.h>
#include <stddef.h>

// Minimal il2cpp runtime API bindings (resolved by dlsym from libil2cpp.so).

struct Il2CppDomain;
struct Il2CppAssembly;
struct Il2CppImage;
struct Il2CppClass;
struct Il2CppObject;
struct Il2CppString;
struct Il2CppType;
struct Il2CppException;

typedef struct Il2CppDomain      Il2CppDomain;
typedef struct Il2CppAssembly    Il2CppAssembly;
typedef struct Il2CppImage       Il2CppImage;
typedef struct Il2CppClass       Il2CppClass;
typedef struct Il2CppType        Il2CppType;
typedef struct Il2CppException   Il2CppException;
typedef struct FieldInfo         FieldInfo;   // Il2CppFieldInfo
typedef struct MethodInfo        MethodInfo;  // Il2CppMethodInfo

// il2cpp object header (klass + monitor) — 16 bytes on ARM64
struct Il2CppObject {
    Il2CppClass* klass;
    void* monitor;
};

struct Il2CppString {
    Il2CppObject obj;
    int32_t length;
    uint16_t chars[1];
};

// API
struct Il2CppApi {
    Il2CppDomain* (*domain_get)(void);
    const Il2CppAssembly** (*domain_get_assemblies)(const Il2CppDomain*, size_t*);
    const Il2CppImage* (*assembly_get_image)(const Il2CppAssembly*);
    const char* (*image_get_name)(const Il2CppImage*);
    Il2CppClass* (*class_from_name)(const Il2CppImage*, const char* ns, const char* name);
    FieldInfo* (*class_get_field_from_name)(Il2CppClass*, const char*);
    const MethodInfo* (*class_get_method_from_name)(Il2CppClass*, const char*, int);
    void (*field_static_get_value)(FieldInfo*, void*);
    void (*field_static_set_value)(FieldInfo*, void*);
    Il2CppString* (*string_new)(const char*);
    Il2CppString* (*string_new_len)(const char*, uint32_t);
    Il2CppObject* (*runtime_invoke)(const MethodInfo*, void* obj, void** params, Il2CppException**);
    Il2CppObject* (*thread_attach)(Il2CppDomain*);
    Il2CppObject* (*value_box)(Il2CppType*, void*);
    Il2CppType* (*class_get_type)(Il2CppClass*);
    Il2CppObject* (*object_new)(Il2CppClass*);
    void (*free)(void*);
    const char* (*class_get_name)(Il2CppClass*);
};

// Resolve the il2cpp API (returns false if libil2cpp.so is not loaded yet).
bool il2cpp_resolve();

// Find a class in the given assembly image by namespace/name.
Il2CppClass* il2cpp_find_class(const char* assembly, const char* ns, const char* name);

// Handle of libil2cpp.so (opened once).
void* il2cpp_lib_handle();

extern Il2CppApi il2cpp;
