cmake_minimum_required(VERSION 3.21)

project(PaperTodoLmdb LANGUAGES C)

if(NOT WIN32)
  message(FATAL_ERROR "PaperTodo's LMDB binary is built for Windows only.")
endif()

set(PAPERTODO_LMDB_SOURCES
  upstream/mdb.c
  upstream/midl.c)

add_library(papertodo_lmdb SHARED
  ${PAPERTODO_LMDB_SOURCES}
  papertodo_lmdb.def)

# Native AOT links LMDB directly into PaperTodo.exe. Keep the DLL target for normal development
# builds and the migration-time WPF executable; both targets are built from the exact same source.
add_library(papertodo_lmdb_static STATIC
  ${PAPERTODO_LMDB_SOURCES})

foreach(target papertodo_lmdb papertodo_lmdb_static)
  target_include_directories(${target} PRIVATE upstream)
  target_compile_features(${target} PRIVATE c_std_11)
  target_compile_definitions(${target} PRIVATE
    _CRT_SECURE_NO_WARNINGS)
endforeach()

if(MSVC)
  foreach(target papertodo_lmdb papertodo_lmdb_static)
    set_property(TARGET ${target} PROPERTY
      MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")
    target_compile_options(${target} PRIVATE
      $<$<CONFIG:Release>:/O1>
      /Gy)
  endforeach()
  target_link_options(papertodo_lmdb PRIVATE
    /OPT:REF
    /OPT:ICF)
endif()

set_target_properties(papertodo_lmdb PROPERTIES
  OUTPUT_NAME "papertodo_lmdb"
  RUNTIME_OUTPUT_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}/bin/win-x64")

set_target_properties(papertodo_lmdb_static PROPERTIES
  OUTPUT_NAME "papertodo_lmdb_static"
  ARCHIVE_OUTPUT_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}/bin/win-x64")

foreach(configuration Debug Release RelWithDebInfo MinSizeRel)
  string(TOUPPER "${configuration}" configuration_upper)
  set_target_properties(papertodo_lmdb PROPERTIES
    "RUNTIME_OUTPUT_DIRECTORY_${configuration_upper}"
    "${CMAKE_CURRENT_SOURCE_DIR}/bin/win-x64")
  set_target_properties(papertodo_lmdb_static PROPERTIES
    "ARCHIVE_OUTPUT_DIRECTORY_${configuration_upper}"
    "${CMAKE_CURRENT_SOURCE_DIR}/bin/win-x64")
endforeach()
