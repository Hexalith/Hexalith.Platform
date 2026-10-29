#define _GNU_SOURCE
#include <dlfcn.h>
#include <errno.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>

static int interrupted;

static void fd_name(int fd, char *target, size_t size)
{
    char link[64];
    snprintf(link, sizeof(link), "/proc/self/fd/%d", fd);
    ssize_t length = readlink(link, target, size - 1);
    target[length < 0 ? 0 : length] = 0;
}

static void observe(const char *event, const char *path)
{
    const char *log = getenv("HEXALITH_NATIVE_PROBE_LOG");
    if (!log) return;
    FILE *file = fopen(log, "a");
    if (!file) return;
    const char *name = strrchr(path, '/');
    fprintf(file, "%s %s\n", event, name ? name + 1 : path);
    fclose(file);
}

int flock(int fd, int operation)
{
    int (*actual)(int, int) = dlsym(RTLD_NEXT, "flock");
    char path[4096];
    fd_name(fd, path, sizeof(path));
    if (operation == 5 && strstr(path, "/metadata-read.json"))
    {
        if (__sync_bool_compare_and_swap(&interrupted, 0, 1))
        {
            observe("injected-EINTR", path);
            errno = EINTR;
            return -1;
        }
        int result = actual(fd, operation);
        if (result == 0) observe("shared-lock-ok", path);
        return result;
    }
    return actual(fd, operation);
}

int fsync(int fd)
{
    int (*actual)(int) = dlsym(RTLD_NEXT, "fsync");
    int result = actual(fd);
    char path[4096];
    fd_name(fd, path, sizeof(path));
    if (result == 0 && (strstr(path, "/.metadata-read.json.") || strstr(path, "/.archive-")))
        observe("fsync-ok-before-rename", path);
    return result;
}
