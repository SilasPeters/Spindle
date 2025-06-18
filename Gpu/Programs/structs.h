#ifndef SPINDLE_STRUCTS
#define SPINDLE_STRUCTS

typedef struct
{
    float3 direction;
    float3 origin;
    float3 accumulated_luminance;
    float3 latest_luminance_sample;
    float3 averaged_samples;
    float t;
    uint material_id;
    uint object_id;
    float sample_count;
} PathState;

typedef struct
{
    float3 camera_position;
    int num_spheres;
    int num_triangles;
} SceneInfo;

typedef struct
{
    float3 position;
    float radius;
    uint material;
} Sphere;

typedef struct
{
    float3 v1;
    float3 v2;
    float3 v3;
    uint material;
} Triangle;

enum MaterialType {
    mat_diffuse = 1u,
    mat_reflective = 2u,
};

typedef struct
{
    float3 color_times_albedo;
    enum MaterialType type;
} Material;

typedef struct
{
    volatile uint extend_ray_length;
    volatile uint shade_diffuse_length;
    volatile uint shade_reflective_length;
    volatile uint shadow_ray_length;
} QueueStates;

#endif
