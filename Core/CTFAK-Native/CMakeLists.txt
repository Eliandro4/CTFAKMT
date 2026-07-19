cmake_minimum_required(VERSION 3.10)
project(CTFAK-Native CXX)

set(CMAKE_CXX_STANDARD 11)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

if(NOT CMAKE_BUILD_TYPE)
    set(CMAKE_BUILD_TYPE Release)
endif()

add_library(CTFAK-Native SHARED
    encryption.cpp
    ImageTranslator.cpp
    tinflate.c
    wrapper.cpp
)

if(WIN32)
    set_target_properties(CTFAK-Native PROPERTIES
        PREFIX ""
        OUTPUT_NAME "CTFAK-Native")
else()
    set_target_properties(CTFAK-Native PROPERTIES
        PREFIX "lib"
        OUTPUT_NAME "CTFAK-Native")
endif()

target_include_directories(CTFAK-Native PRIVATE ${CMAKE_CURRENT_SOURCE_DIR})
